using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static CharacterTestRuntimeAiLabFactsFactViewModel[] BuildFactsRows(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabFactObservationTrace> facts = trace.Facts;
            var result = new CharacterTestRuntimeAiLabFactsFactViewModel[facts.Count];
            int output = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < facts.Count; i++)
                {
                    CharacterTestRuntimeAiLabFactObservationTrace fact = facts[i];
                    bool pinned = controller.IsFactsPinned(fact.StableId);
                    if ((pass == 0) != pinned)
                        continue;
                    bool changed = fact.BaselineUnknown || !fact.Value.Equals(fact.PreviousValue);
                    bool participated = fact.ParticipatedLayers != CharacterTestRuntimeAiLabDecisionTraceLayer.None;
                    bool selected = IsFactSelectedByCausalLink(trace, controller.FactsSelectedStableId, fact.StableId);
                    string displayName = fact.Domain == CharacterTestRuntimeAiLabDecisionTraceFactDomain.RuntimeAiPlanner
                        ? fact.PlannerFact.ToString()
                        : fact.Fact.ToString();
                    bool searchMatch = string.IsNullOrEmpty(controller.FactsSearchText)
                        || fact.StableId.IndexOf(controller.FactsSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                        || displayName.IndexOf(controller.FactsSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    result[output++] = new CharacterTestRuntimeAiLabFactsFactViewModel
                    {
                        StableId = fact.StableId ?? string.Empty,
                        DisplayName = displayName,
                        GroupText = ResolveFactGroup(fact),
                        ValueText = FormatTraceValue(fact.Value),
                        PreviousValueText = fact.BaselineUnknown
                            ? "基线未知"
                            : FormatTraceValue(fact.PreviousValue),
                        SourceText = fact.SourceUnknown
                            ? "未知来源 · FACT_SOURCE_UNKNOWN"
                            : NonEmpty(fact.SourceStableId, "未知来源")
                                + " @F" + fact.SourceFrame.ToString(CultureInfo.InvariantCulture),
                        FreshnessText = fact.SourceUnknown
                            ? "未知"
                            : Math.Max(0, fact.ObservedFrame - fact.SourceFrame)
                                .ToString(CultureInfo.InvariantCulture) + "f",
                        AuthorityText = fact.AuthorityKind.ToString(),
                        ChangeText = fact.BaselineUnknown ? "建立基线" : changed ? "已变化" : "未变化",
                        ParticipationText = participated ? fact.ParticipatedLayers.ToString() : "未参与",
                        LastChangedFrame = fact.LastChangedFrame,
                        ParticipatedLayers = fact.ParticipatedLayers,
                        Changed = changed,
                        Pinned = pinned,
                        Selected = selected,
                        Visible = searchMatch
                            && (!controller.FactsChangedOnly || changed)
                            && (!controller.FactsParticipatedOnly || participated),
                        Status = selected
                            ? CharacterTestRuntimeAiLabFactsStatus.Success
                            : participated
                                ? CharacterTestRuntimeAiLabFactsStatus.Active
                                : CharacterTestRuntimeAiLabFactsStatus.Neutral
                    };
                }
            }
            return result;
        }

        private static CharacterTestRuntimeAiLabFactsCandidateViewModel[] BuildCandidateRows(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string selectedStableId,
            string searchText)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabCandidateEvaluationTrace> candidates = trace.Candidates;
            var result = new CharacterTestRuntimeAiLabFactsCandidateViewModel[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = candidates[i];
                var related = new string[candidate.RelatedFactCount];
                var relatedText = new StringBuilder(candidate.RelatedFactCount * 28);
                for (int link = 0; link < candidate.RelatedFactCount; link++)
                {
                    string id = candidate.GetRelatedFactStableId(link);
                    related[link] = id;
                    if (link > 0)
                        relatedText.Append(" · ");
                    relatedText.Append(id);
                }
                bool causallySelected = string.Equals(candidate.StableId, selectedStableId, StringComparison.Ordinal)
                    || ContainsStableId(related, selectedStableId);
                result[i] = new CharacterTestRuntimeAiLabFactsCandidateViewModel
                {
                    StableId = candidate.StableId ?? string.Empty,
                    DisplayName = candidate.StableId ?? string.Empty,
                    LayerText = candidate.Layer.ToString(),
                    StateText = candidate.State.ToString(),
                    ScoreText = candidate.ScoreAvailable
                        ? candidate.Score.ToString("0.###", CultureInfo.InvariantCulture)
                        : "不可用",
                    PriorityText = candidate.PriorityAvailable
                        ? candidate.Priority.ToString(CultureInfo.InvariantCulture)
                        : "不可用",
                    CostText = candidate.CostAvailable
                        ? candidate.Cost.ToString("0.###", CultureInfo.InvariantCulture)
                        : "不可用",
                    ReasonCode = candidate.ReasonCode.ToString(CultureInfo.InvariantCulture),
                    RelatedFactsText = relatedText.ToString(),
                    RelatedFactStableIds = related,
                    StableRank = candidate.StableRank,
                    Selected = causallySelected,
                    Visible = string.IsNullOrEmpty(searchText)
                        || (candidate.StableId?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                        || candidate.ReasonCode.ToString(CultureInfo.InvariantCulture).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                        || relatedText.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0,
                    Status = candidate.State == CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected
                        ? CharacterTestRuntimeAiLabFactsStatus.Success
                        : candidate.State == CharacterTestRuntimeAiLabDecisionTraceCandidateState.Ineligible
                            || candidate.State == CharacterTestRuntimeAiLabDecisionTraceCandidateState.Suppressed
                                ? CharacterTestRuntimeAiLabFactsStatus.Warning
                                : CharacterTestRuntimeAiLabFactsStatus.Neutral
                };
            }
            return result;
        }

        private static CharacterTestRuntimeAiLabFactsFactViewModel[] BuildDeltaRows(
            CharacterTestRuntimeAiLabFactsFactViewModel[] facts)
        {
            int count = 0;
            for (int i = 0; facts != null && i < facts.Length; i++)
            {
                if (facts[i].Changed)
                    count++;
            }
            var result = new CharacterTestRuntimeAiLabFactsFactViewModel[count];
            int output = 0;
            for (int i = 0; facts != null && i < facts.Length; i++)
            {
                if (facts[i].Changed)
                    result[output++] = facts[i];
            }
            return result;
        }

        private static CharacterTestRuntimeAiLabFactsCausalStepViewModel[] BuildCausalFlow(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string selectedStableId)
        {
            CharacterTestRuntimeAiLabCommittedStateTrace previous = trace.PreviousCommitted;
            CharacterTestRuntimeAiLabCommitTrace commit = trace.Commit;
            string participating = BuildParticipatingFactsSummary(trace, 4);
            string selected = BuildSelectedCandidatesSummary(trace, 3);
            bool winningPathSelected = IsWinningPathSelection(trace, selectedStableId);
            return new[]
            {
                Causal("previous", "1", "上一提交", NonEmpty(previous.CardStableId, "无行为卡"),
                    NonEmpty(previous.StageStableId, "无阶段") + " · "
                        + NonEmpty(previous.MainActionStableId, "无主动作"),
                    CharacterTestRuntimeAiLabFactsStatus.Neutral,
                    winningPathSelected || MatchesCommitted(previous, selectedStableId)),
                Causal("facts", "2", "本帧观察", participating,
                    trace.Facts.Count.ToString(CultureInfo.InvariantCulture) + " 项 typed facts 已密封",
                    CharacterTestRuntimeAiLabFactsStatus.Active,
                    winningPathSelected || IsFactStableId(trace, selectedStableId)),
                Causal("arbitration", "3", "候选裁决", selected,
                    trace.Candidates.Count.ToString(CultureInfo.InvariantCulture) + " 个实际候选",
                    CharacterTestRuntimeAiLabFactsStatus.Success,
                    winningPathSelected || IsCandidateStableId(trace, selectedStableId)),
                Causal("commit", "4", "分阶段提交", commit.PlannerLogicKind.ToString(),
                    commit.CommandEnqueueKind + " · " + commit.RuntimeEffectKind,
                    commit.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected
                        ? CharacterTestRuntimeAiLabFactsStatus.Error
                        : CharacterTestRuntimeAiLabFactsStatus.Active,
                    winningPathSelected || MatchesCommit(commit, selectedStableId))
            };
        }

        private static CharacterTestRuntimeAiLabFactsCommitPhaseViewModel[] BuildCommitPhases(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string selectedStableId)
        {
            CharacterTestRuntimeAiLabCommitTrace commit = trace.Commit;
            bool selected = IsWinningPathSelection(trace, selectedStableId)
                || MatchesCommit(commit, selectedStableId);
            return new[]
            {
                CommitPhase("planner", "Planner / Logic", commit.PlannerLogicKind.ToString(),
                    commit.PlannerReasonCode.ToString(CultureInfo.InvariantCulture), commit.PlannerEvidenceFrame,
                    commit.PlannerLogicKind == CharacterTestRuntimeAiLabPlannerLogicCommitKind.Rejected
                        ? CharacterTestRuntimeAiLabFactsStatus.Error
                        : CharacterTestRuntimeAiLabFactsStatus.Active,
                    selected),
                CommitPhase("command", "Command 入队", commit.CommandEnqueueKind.ToString(),
                    commit.CommandReasonCode.ToString(CultureInfo.InvariantCulture), commit.CommandEvidenceFrame,
                    commit.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected
                        ? CharacterTestRuntimeAiLabFactsStatus.Error
                        : CharacterTestRuntimeAiLabFactsStatus.Neutral,
                    selected),
                CommitPhase("effect", "Runtime Effect", commit.RuntimeEffectKind.ToString(),
                    string.IsNullOrEmpty(commit.RuntimeEffectActionStableId)
                        ? commit.RuntimeEffectReasonCode.ToString(CultureInfo.InvariantCulture)
                        : commit.RuntimeEffectReasonCode + " · " + commit.RuntimeEffectActionStableId,
                    commit.RuntimeEffectEvidenceFrame,
                    commit.RuntimeEffectKind == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Failed
                        ? CharacterTestRuntimeAiLabFactsStatus.Error
                        : commit.RuntimeEffectKind == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved
                            ? CharacterTestRuntimeAiLabFactsStatus.Neutral
                            : CharacterTestRuntimeAiLabFactsStatus.Success,
                    selected)
            };
        }

        private static CharacterTestRuntimeAiLabFactsCausalStepViewModel Causal(
            string id, string number, string title, string primary, string detail,
            CharacterTestRuntimeAiLabFactsStatus status,
            bool selected)
        {
            return new CharacterTestRuntimeAiLabFactsCausalStepViewModel
            {
                StableId = id,
                StepNumber = number,
                Title = title,
                PrimaryText = primary,
                DetailText = detail,
                Status = status,
                Selected = selected
            };
        }

        private static CharacterTestRuntimeAiLabFactsCommitPhaseViewModel CommitPhase(
            string id, string title, string state, string reason, int frame,
            CharacterTestRuntimeAiLabFactsStatus status,
            bool selected)
        {
            return new CharacterTestRuntimeAiLabFactsCommitPhaseViewModel
            {
                StableId = id,
                Title = title,
                StateCode = state,
                ReasonCode = reason ?? string.Empty,
                EvidenceFrame = frame,
                Status = status,
                Selected = selected
            };
        }

        private static bool IsFactStableId(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string stableId)
        {
            for (int i = 0; !string.IsNullOrEmpty(stableId) && i < trace.Facts.Count; i++)
            {
                if (string.Equals(trace.Facts[i].StableId, stableId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool IsCandidateStableId(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string stableId)
        {
            for (int i = 0; !string.IsNullOrEmpty(stableId) && i < trace.Candidates.Count; i++)
            {
                if (string.Equals(trace.Candidates[i].StableId, stableId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool MatchesCommitted(
            in CharacterTestRuntimeAiLabCommittedStateTrace state,
            string stableId) =>
            !string.IsNullOrEmpty(stableId)
            && (string.Equals(state.CardStableId, stableId, StringComparison.Ordinal)
                || string.Equals(state.StageStableId, stableId, StringComparison.Ordinal)
                || string.Equals(state.MainActionStableId, stableId, StringComparison.Ordinal)
                || string.Equals(state.PendingTransitionStableId, stableId, StringComparison.Ordinal));

        private static bool MatchesCommit(
            in CharacterTestRuntimeAiLabCommitTrace commit,
            string stableId) =>
            !string.IsNullOrEmpty(stableId)
            && (string.Equals(commit.CardStableId, stableId, StringComparison.Ordinal)
                || string.Equals(commit.StageStableId, stableId, StringComparison.Ordinal)
                || string.Equals(commit.MainActionStableId, stableId, StringComparison.Ordinal)
                || string.Equals(commit.TransitionStableId, stableId, StringComparison.Ordinal));

        private static string BuildParticipatingFactsSummary(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            int maximum)
        {
            var text = new StringBuilder(128);
            int count = 0;
            for (int i = 0; i < trace.Facts.Count; i++)
            {
                CharacterTestRuntimeAiLabFactObservationTrace fact = trace.Facts[i];
                if (fact.ParticipatedLayers == CharacterTestRuntimeAiLabDecisionTraceLayer.None)
                    continue;
                if (count < maximum)
                {
                    if (text.Length > 0)
                        text.Append(" · ");
                    text.Append(fact.StableId);
                }
                count++;
            }
            if (count == 0)
                return "无参与事实";
            if (count > maximum)
                text.Append(" +").Append(count - maximum);
            return text.ToString();
        }

        private static string BuildSelectedCandidatesSummary(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            int maximum)
        {
            var text = new StringBuilder(128);
            int count = 0;
            for (int i = 0; i < trace.Candidates.Count; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[i];
                if (candidate.State != CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected)
                    continue;
                if (count < maximum)
                {
                    if (text.Length > 0)
                        text.Append(" → ");
                    text.Append(candidate.StableId);
                }
                count++;
            }
            if (count == 0)
                return "无胜出候选";
            if (count > maximum)
                text.Append(" +").Append(count - maximum);
            return text.ToString();
        }

        private static bool IsFactSelectedByCausalLink(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string selection,
            string factStableId)
        {
            if (string.Equals(selection, factStableId, StringComparison.Ordinal))
                return true;
            for (int i = 0; i < trace.Candidates.Count; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[i];
                if (!string.Equals(candidate.StableId, selection, StringComparison.Ordinal))
                    continue;
                for (int link = 0; link < candidate.RelatedFactCount; link++)
                {
                    if (string.Equals(candidate.GetRelatedFactStableId(link), factStableId, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static bool IsWinningPathSelection(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string selectedStableId)
        {
            if (string.IsNullOrEmpty(selectedStableId))
                return false;
            if (MatchesCommitted(trace.PreviousCommitted, selectedStableId)
                || MatchesCommit(trace.Commit, selectedStableId))
            {
                return true;
            }
            for (int i = 0; i < trace.Candidates.Count; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[i];
                if (candidate.State != CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected)
                    continue;
                if (string.Equals(candidate.StableId, selectedStableId, StringComparison.Ordinal))
                    return true;
                for (int link = 0; link < candidate.RelatedFactCount; link++)
                {
                    if (string.Equals(
                            candidate.GetRelatedFactStableId(link),
                            selectedStableId,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static string ResolveFactGroup(in CharacterTestRuntimeAiLabFactObservationTrace fact)
        {
            if (fact.Domain == CharacterTestRuntimeAiLabDecisionTraceFactDomain.BehaviorCardLogic)
                return "行为卡逻辑";
            switch (fact.PlannerFact)
            {
                case CharacterTestRuntimeAiPlannerFactKind.TargetExists:
                case CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange:
                case CharacterTestRuntimeAiPlannerFactKind.TargetDistance:
                case CharacterTestRuntimeAiPlannerFactKind.TargetVisible:
                case CharacterTestRuntimeAiPlannerFactKind.TargetDead:
                case CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked:
                case CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames:
                case CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore:
                    return "目标 / 感知";
                case CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning:
                case CharacterTestRuntimeAiPlannerFactKind.SelfSurfaceKind:
                case CharacterTestRuntimeAiPlannerFactKind.SelfOnHardSlick:
                case CharacterTestRuntimeAiPlannerFactKind.SelfNearMovableObject:
                case CharacterTestRuntimeAiPlannerFactKind.SelfDodgeCooldownReady:
                    return "自身状态";
                case CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable:
                case CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear:
                case CharacterTestRuntimeAiPlannerFactKind.MoveCandidateIndex:
                    return "移动 / Planner";
                default:
                    return "环境 / 战斗";
            }
        }

        private static string FormatTraceValue(in CharacterTestRuntimeAiLabDecisionTraceValue value)
        {
            switch (value.Kind)
            {
                case CharacterTestRuntimeAiLabDecisionTraceValueKind.Boolean:
                    return value.Boolean ? "true" : "false";
                case CharacterTestRuntimeAiLabDecisionTraceValueKind.Integer:
                    return value.Integer.ToString(CultureInfo.InvariantCulture);
                case CharacterTestRuntimeAiLabDecisionTraceValueKind.Float:
                    return value.Number.ToString("0.###", CultureInfo.InvariantCulture);
                case CharacterTestRuntimeAiLabDecisionTraceValueKind.Text:
                    return NonEmpty(value.Text, "空文本");
                default:
                    return "不可用";
            }
        }

        private static bool ContainsStableId(string[] values, string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
                return false;
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (string.Equals(values[i], stableId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
