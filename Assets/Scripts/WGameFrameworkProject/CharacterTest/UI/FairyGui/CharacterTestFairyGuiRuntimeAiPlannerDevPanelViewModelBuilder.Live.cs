using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static void FillFactsWorkspace(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot,
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame)
        {
            var factsView = new CharacterTestRuntimeAiLabFactsWorkspaceViewModel();
            viewModel.Facts = factsView;
            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt = snapshot.CommittedFrameReceipt;
            CharacterTestRuntimeAiPlannerDevTimeline timeline = snapshot.Timeline;
            factsView.SearchText = controller.FactsSearchText;
            factsView.ChangedOnly = controller.FactsChangedOnly;
            factsView.ParticipatedOnly = controller.FactsParticipatedOnly;
            factsView.DeltaExpanded = controller.FactsDeltaExpanded;
            factsView.FactScrollOffset = (int)controller.FactsFactScrollOffset;
            factsView.CandidateScrollOffset = (int)controller.FactsCandidateScrollOffset;
            factsView.DeltaScrollOffset = (int)controller.FactsDeltaScrollOffset;
            factsView.ActiveTableTab = controller.FactsActiveTableTab;
            factsView.SelectedStableId = controller.FactsSelectedStableId;
            if (controller.TryGetActiveReplayFactsCausalSnapshot(
                    out CharacterTestRuntimeAiLabReplayCausalSnapshot replayCausal)
                && replayCausal.TryGetTrace(out CharacterTestRuntimeAiLabDecisionTraceAgentView replayTrace))
            {
                FillFrozenReplayFactsWorkspace(factsView, controller, replayCausal, replayTrace);
                return;
            }
            factsView.LatestFrame = timeline?.LatestFrame ?? 0;
            factsView.RetainedStartFrame = timeline?.RetainedStartFrame ?? 0;
            factsView.CommitGeneration = receipt.CommitGeneration;
            factsView.AuthorityHash = receipt.AuthorityHash;
            factsView.SessionGeneration = receipt.Bundle?.SessionIdentity.Generation ?? 0L;
            factsView.AppliedRevisionHash = receipt.Bundle?.AppliedBehaviorRevisionHash ?? 0L;
            factsView.RevisionText = "Session "
                + factsView.SessionGeneration.ToString(CultureInfo.InvariantCulture)
                + " · Rev "
                + ((ulong)factsView.AppliedRevisionHash).ToString("X8", CultureInfo.InvariantCulture);
            factsView.CanReturnSourceFrame = controller.HasFactsNavigationReturnToken;
            factsView.DecisionFrame = -1;

            factsView.PreferredAgentEntityId = receipt.PreferredAgentEntityId;
            factsView.CommittedAgentEntityId = receipt.ResolvedAgentEntityId;
            factsView.Frame = receipt.Frame;
            factsView.Agents = BuildFactsAgents(agents, receipt.ResolvedAgentEntityId);
            factsView.ContextText = BuildFactsContextText(factsView, snapshot.FrameSelectionPhase);
            factsView.HistoryStateText = factsView.Frame < factsView.LatestFrame
                ? "历史回看"
                : "最新帧";

            if (!receipt.IsValid || receipt.Bundle?.Record == null)
            {
                SetFactsUnavailable(factsView, "FACT_TRACE_UNAVAILABLE");
                return;
            }
            CharacterTestRuntimeAiPlannerDevTimelineRecord record = receipt.Bundle.Record;
            if (record.Frame != receipt.Frame
                || record.RecordGeneration != receipt.Bundle.RecordGeneration
                || selectedFrame == null
                || selectedFrame.Frame != receipt.Frame)
            {
                SetFactsUnavailable(factsView, "FACT_TRACE_FRAME_MISMATCH");
                return;
            }
            if (receipt.ResolvedAgentEntityId <= 0)
            {
                SetFactsUnavailable(factsView, "FACT_TRACE_AGENT_MISSING");
                return;
            }
            CharacterTestRuntimeAiLabDecisionTraceHandle handle = record.DecisionTraceHandle;
            if (!handle.IsValid)
            {
                SetFactsUnavailable(factsView, "FACT_TRACE_UNAVAILABLE");
                return;
            }
            if (handle.Frame != receipt.Frame
                || handle.SessionGeneration != receipt.Bundle.SessionIdentity.Generation)
            {
                SetFactsUnavailable(factsView, "FACT_TRACE_FRAME_MISMATCH");
                return;
            }
            factsView.DecisionFrame = handle.DecisionFrame;
            factsView.ContextText = BuildFactsContextText(factsView, snapshot.FrameSelectionPhase);
            if (timeline == null
                || !timeline.TryGetDecisionTrace(record, receipt.ResolvedAgentEntityId, out var trace))
            {
                SetFactsUnavailable(factsView, "FACT_TRACE_AGENT_MISSING");
                return;
            }

            controller.ClearFactsSelectionIfUnavailable(trace.Facts, trace.Candidates);
            factsView.SelectedStableId = controller.FactsSelectedStableId;
            factsView.Available = true;
            factsView.DiagnosticCode = trace.Overflowed ? "FACT_TRACE_OVERFLOW" : string.Empty;
            factsView.FactRows = BuildFactsRows(trace, controller);
            factsView.CandidateRows = BuildCandidateRows(
                trace,
                controller.FactsSelectedStableId,
                controller.FactsSearchText);
            factsView.DeltaRows = BuildDeltaRows(factsView.FactRows);
            factsView.CausalFlow = BuildCausalFlow(trace, controller.FactsSelectedStableId);
            ApplyNavigationDiagnosticsToFactsCausalFlow(
                factsView.CausalFlow,
                agents,
                receipt.ResolvedAgentEntityId);
            factsView.CommitPhases = BuildCommitPhases(trace, controller.FactsSelectedStableId);
            factsView.CanLocateBehaviorNode =
                CharacterTestRuntimeAiLabFactsBehaviorTargetResolver.TryResolve(
                    trace,
                    controller.FactsSelectedStableId,
                    out _);
            factsView.CanLocateRuntimeFrame = receipt.IsValid;
        }

        private static void ApplyNavigationDiagnosticsToFactsCausalFlow(
            CharacterTestRuntimeAiLabFactsCausalStepViewModel[] causalFlow,
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents,
            int entityId)
        {
            if (causalFlow == null || causalFlow.Length < 2 || agents == null || entityId <= 0)
                return;

            for (int i = 0; i < agents.Count; i++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[i];
                if (agent.EntityId != entityId || !agent.NavigationDiagnostics.HasSample)
                    continue;

                CharacterTestRuntimeAiLabFactsCausalStepViewModel observation = causalFlow[1];
                observation.Title = "本帧观察 · Path";
                observation.PrimaryText =
                    CharacterTestRuntimeAiPlannerNavigationDiagnosticText.BuildFactsCardPrimary(agent);
                observation.DetailText =
                    CharacterTestRuntimeAiPlannerNavigationDiagnosticText.BuildFactsCardDetail(agent);
                return;
            }
        }

        private static void FillFrozenReplayFactsWorkspace(
            CharacterTestRuntimeAiLabFactsWorkspaceViewModel factsView,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabReplayCausalSnapshot causal,
            CharacterTestRuntimeAiLabDecisionTraceAgentView trace)
        {
            factsView.LatestFrame = causal.LatestFrame;
            factsView.RetainedStartFrame = causal.RetainedStartFrame;
            factsView.CommitGeneration = causal.CaptureGeneration;
            factsView.AuthorityHash = causal.AuthorityHash;
            factsView.SessionGeneration = causal.SessionGeneration;
            factsView.AppliedRevisionHash = causal.AppliedRevisionHash;
            factsView.RevisionText = "Frozen Replay · Capture "
                + causal.CaptureGeneration.ToString(CultureInfo.InvariantCulture)
                + " · Rev "
                + ((ulong)causal.AppliedRevisionHash).ToString("X8", CultureInfo.InvariantCulture);
            factsView.CanReturnSourceFrame = true;
            factsView.DecisionFrame = causal.ReplayFrame;
            factsView.PreferredAgentEntityId = causal.AgentEntityId;
            factsView.CommittedAgentEntityId = causal.AgentEntityId;
            factsView.Frame = causal.ReplayFrame;
            factsView.Agents = new[]
            {
                new CharacterTestRuntimeAiLabFactsAgentViewModel
                {
                    EntityId = causal.AgentEntityId,
                    DisplayName = "Agent " + causal.AgentEntityId.ToString(CultureInfo.InvariantCulture),
                    StateText = "Frozen Replay causal evidence",
                    Selected = true
                }
            };
            factsView.ContextText = "Agent "
                + causal.AgentEntityId.ToString(CultureInfo.InvariantCulture)
                + " · Replay " + causal.ReplayFrame.ToString(CultureInfo.InvariantCulture)
                + " / RuntimeFrame " + causal.LiveFrame.ToString(CultureInfo.InvariantCulture)
                + "\nFrozen H " + causal.AuthorityHash.ToString("X16", CultureInfo.InvariantCulture)
                + " · Evidence " + causal.TimelineEvidenceHash.ToString("X16", CultureInfo.InvariantCulture)
                + "\n" + factsView.RevisionText;
            factsView.HistoryStateText = "Replay Frozen";

            controller.ClearFactsSelectionIfUnavailable(trace.Facts, trace.Candidates);
            factsView.SelectedStableId = controller.FactsSelectedStableId;
            factsView.Available = true;
            factsView.DiagnosticCode = trace.Overflowed
                ? "REPLAY_CAUSAL_TRACE_OVERFLOW"
                : string.Empty;
            factsView.FactRows = BuildFactsRows(trace, controller);
            factsView.CandidateRows = BuildCandidateRows(
                trace,
                controller.FactsSelectedStableId,
                controller.FactsSearchText);
            factsView.DeltaRows = BuildDeltaRows(factsView.FactRows);
            factsView.CausalFlow = BuildCausalFlow(trace, controller.FactsSelectedStableId);
            factsView.CommitPhases = BuildCommitPhases(trace, controller.FactsSelectedStableId);
            factsView.CanLocateBehaviorNode =
                CharacterTestRuntimeAiLabFactsBehaviorTargetResolver.TryResolve(
                    trace,
                    controller.FactsSelectedStableId,
                    out _);
            factsView.CanLocateRuntimeFrame = false;
        }

        private static CharacterTestRuntimeAiLabFactsAgentViewModel[] BuildFactsAgents(
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents,
            int committedAgentEntityId)
        {
            int count = agents?.Count ?? 0;
            var result = new CharacterTestRuntimeAiLabFactsAgentViewModel[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[i];
                result[i] = new CharacterTestRuntimeAiLabFactsAgentViewModel
                {
                    EntityId = agent.EntityId,
                    DisplayName = "Agent " + agent.EntityId.ToString(CultureInfo.InvariantCulture),
                    StateText = NonEmpty(agent.ActiveStatePath, "无活动状态"),
                    Selected = agent.EntityId == committedAgentEntityId
                };
            }
            return result;
        }

        private static string BuildFactsContextText(
            CharacterTestRuntimeAiLabFactsWorkspaceViewModel view,
            CharacterTestRuntimeAiLabFrameSelectionPhase phase)
        {
            string preferredFallback = view.PreferredAgentEntityId > 0
                && view.PreferredAgentEntityId != view.CommittedAgentEntityId
                    ? " · 首选 Agent " + view.PreferredAgentEntityId.ToString(CultureInfo.InvariantCulture)
                        + " 不在该帧"
                    : string.Empty;
            return "Agent " + view.CommittedAgentEntityId.ToString(CultureInfo.InvariantCulture)
                + " · S" + view.Frame.ToString(CultureInfo.InvariantCulture)
                + "/D" + (view.DecisionFrame >= 0
                    ? view.DecisionFrame.ToString(CultureInfo.InvariantCulture)
                    : "—")
                + "/L" + view.LatestFrame.ToString(CultureInfo.InvariantCulture)
                + preferredFallback
                + "\nH " + view.AuthorityHash.ToString("X16", CultureInfo.InvariantCulture)
                + " · C" + view.CommitGeneration.ToString(CultureInfo.InvariantCulture)
                + "\n" + view.RevisionText
                + (phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Idle
                    ? string.Empty
                    : " · " + phase);
        }

        private static void SetFactsUnavailable(
            CharacterTestRuntimeAiLabFactsWorkspaceViewModel view,
            string code)
        {
            view.Available = false;
            view.DiagnosticCode = code;
            view.CausalFlow = new[]
            {
                new CharacterTestRuntimeAiLabFactsCausalStepViewModel
                {
                    StableId = code,
                    StepNumber = "!",
                    Title = "诊断轨迹不可用",
                    PrimaryText = code,
                    DetailText = "页面保持当前 committed frame，不读取最新 evaluator 推断数据。",
                    Status = CharacterTestRuntimeAiLabFactsStatus.Warning
                }
            };
        }
    }
}
