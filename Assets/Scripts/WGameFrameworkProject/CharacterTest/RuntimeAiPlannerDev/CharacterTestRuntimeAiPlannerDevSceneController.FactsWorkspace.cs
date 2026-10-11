using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private readonly List<string> _factsPinnedStableIds = new List<string>(16);
        private string _factsSearchText = string.Empty;
        private string _factsSelectedStableId = string.Empty;
        private bool _factsChangedOnly;
        private bool _factsParticipatedOnly;
        private bool _factsDeltaExpanded;
        private CharacterTestRuntimeAiLabFactsTableTab _factsActiveTableTab;
        private float _factsFactScrollOffset;
        private float _factsCandidateScrollOffset;
        private float _factsDeltaScrollOffset;
        private long _factsPresentationRevision;
        private bool _factsNavigationReturnValid;
        private int _factsNavigationReturnFrame;
        private int _factsNavigationReturnAgentEntityId;
        private long _factsNavigationReturnRecordGeneration;
        private long _factsNavigationReturnSessionGeneration;

        internal string FactsSearchText => _factsSearchText;
        internal string FactsSelectedStableId => _factsSelectedStableId;
        internal bool FactsChangedOnly => _factsChangedOnly;
        internal bool FactsParticipatedOnly => _factsParticipatedOnly;
        internal bool FactsDeltaExpanded => _factsDeltaExpanded;
        internal CharacterTestRuntimeAiLabFactsTableTab FactsActiveTableTab => _factsActiveTableTab;
        internal float FactsFactScrollOffset => _factsFactScrollOffset;
        internal float FactsCandidateScrollOffset => _factsCandidateScrollOffset;
        internal float FactsDeltaScrollOffset => _factsDeltaScrollOffset;
        internal long FactsPresentationRevision => _factsPresentationRevision;

        internal bool IsFactsPinned(string stableId)
        {
            for (int i = 0; i < _factsPinnedStableIds.Count; i++)
            {
                if (string.Equals(_factsPinnedStableIds[i], stableId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal void SetFactsSearchText(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (string.Equals(_factsSearchText, normalized, StringComparison.Ordinal))
                return;
            _factsSearchText = normalized;
            _factsPresentationRevision++;
        }

        internal void ToggleFactsChangedOnly()
        {
            _factsChangedOnly = !_factsChangedOnly;
            _factsPresentationRevision++;
        }

        internal void ToggleFactsParticipatedOnly()
        {
            _factsParticipatedOnly = !_factsParticipatedOnly;
            _factsPresentationRevision++;
        }

        internal void ToggleFactsDeltaExpanded()
        {
            _factsDeltaExpanded = !_factsDeltaExpanded;
            _factsPresentationRevision++;
        }

        internal void SetFactsActiveTableTab(CharacterTestRuntimeAiLabFactsTableTab tab)
        {
            if (_factsActiveTableTab == tab)
                return;
            _factsActiveTableTab = tab;
            _factsPresentationRevision++;
        }

        internal void SelectFactsStableId(string stableId)
        {
            string normalized = stableId ?? string.Empty;
            if (string.Equals(_factsSelectedStableId, normalized, StringComparison.Ordinal))
                return;
            _factsSelectedStableId = normalized;
            _factsPresentationRevision++;
        }

        internal void ClearFactsSelectionIfUnavailable(
            IReadOnlyList<CharacterTestRuntimeAiLabFactObservationTrace> facts,
            IReadOnlyList<CharacterTestRuntimeAiLabCandidateEvaluationTrace> candidates)
        {
            if (string.IsNullOrEmpty(_factsSelectedStableId))
                return;
            for (int i = 0; facts != null && i < facts.Count; i++)
            {
                if (string.Equals(facts[i].StableId, _factsSelectedStableId, StringComparison.Ordinal))
                    return;
            }
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i].StableId, _factsSelectedStableId, StringComparison.Ordinal))
                    return;
            }
            _factsSelectedStableId = string.Empty;
            _factsPresentationRevision++;
        }

        internal void ToggleFactsPin(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return;
            for (int i = 0; i < _factsPinnedStableIds.Count; i++)
            {
                if (!string.Equals(_factsPinnedStableIds[i], stableId, StringComparison.Ordinal))
                    continue;
                _factsPinnedStableIds.RemoveAt(i);
                _factsPresentationRevision++;
                return;
            }
            _factsPinnedStableIds.Add(stableId);
            _factsPresentationRevision++;
        }

        internal void SetFactsScrollOffsets(float fact, float candidate, float delta)
        {
            float normalizedFact = Math.Max(0f, fact);
            float normalizedCandidate = Math.Max(0f, candidate);
            float normalizedDelta = Math.Max(0f, delta);
            if (_factsFactScrollOffset.Equals(normalizedFact)
                && _factsCandidateScrollOffset.Equals(normalizedCandidate)
                && _factsDeltaScrollOffset.Equals(normalizedDelta))
            {
                return;
            }
            _factsFactScrollOffset = normalizedFact;
            _factsCandidateScrollOffset = normalizedCandidate;
            _factsDeltaScrollOffset = normalizedDelta;
            _factsPresentationRevision++;
        }

        internal bool SelectFactsAgent(int entityId, out string diagnostic)
        {
            diagnostic = string.Empty;
            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt =
                _frameSelection.CommittedReceipt;
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents =
                receipt.Bundle?.Record?.FrameSnapshot?.Agents;
            int agentIndex = -1;
            for (int i = 0; agents != null && i < agents.Count; i++)
            {
                if (agents[i].EntityId == entityId)
                {
                    agentIndex = i;
                    break;
                }
            }
            if (!receipt.IsValid || agentIndex < 0 || !SelectAgent(agentIndex))
            {
                diagnostic = "FACT_AGENT_SELECTION_UNAVAILABLE";
                return false;
            }
            if (!_frameSelection.CommitLiveFrame(entityId, receipt.Bundle, out _, entityId))
            {
                diagnostic = "FACT_AGENT_SELECTION_COMMIT_FAILED";
                return false;
            }
            _factsPresentationRevision++;
            diagnostic = "Facts Agent selection committed.";
            return true;
        }

        internal void CaptureFactsNavigationReturnToken()
        {
            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt = _frameSelection.CommittedReceipt;
            _factsNavigationReturnValid = receipt.IsValid;
            if (!_factsNavigationReturnValid)
                return;
            _factsNavigationReturnFrame = receipt.Frame;
            _factsNavigationReturnAgentEntityId = receipt.PreferredAgentEntityId;
            _factsNavigationReturnRecordGeneration = receipt.Bundle.RecordGeneration;
            _factsNavigationReturnSessionGeneration = receipt.Bundle.SessionIdentity.Generation;
        }

        internal bool RestoreFactsNavigationReturnToken(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_factsNavigationReturnValid)
                return true;
            if (_timeline == null
                || _timeline.SessionIdentity.Generation != _factsNavigationReturnSessionGeneration
                || !_timeline.TryGetFrame(_factsNavigationReturnFrame, out var record)
                || record.RecordGeneration != _factsNavigationReturnRecordGeneration)
            {
                _factsNavigationReturnValid = false;
                diagnostic = "FACT_RETURN_SOURCE_STALE";
                return false;
            }
            if (_frameSelection.CommittedReceipt.IsValid
                && _frameSelection.CommittedReceipt.Frame == _factsNavigationReturnFrame
                && _frameSelection.CommittedReceipt.PreferredAgentEntityId == _factsNavigationReturnAgentEntityId)
            {
                return true;
            }
            bool started = JumpToFrame(
                _factsNavigationReturnFrame,
                _factsNavigationReturnAgentEntityId);
            diagnostic = started ? "Facts source frame restore started." : "FACT_RETURN_SOURCE_RESTORE_FAILED";
            return started;
        }

        internal bool HasFactsNavigationReturnToken => _factsNavigationReturnValid;

        internal void ResetFactsWorkspaceTransientContext()
        {
            _factsPinnedStableIds.Clear();
            _factsSearchText = string.Empty;
            _factsSelectedStableId = string.Empty;
            _factsChangedOnly = false;
            _factsParticipatedOnly = false;
            _factsDeltaExpanded = false;
            _factsActiveTableTab = CharacterTestRuntimeAiLabFactsTableTab.Facts;
            _factsFactScrollOffset = 0f;
            _factsCandidateScrollOffset = 0f;
            _factsDeltaScrollOffset = 0f;
            _factsNavigationReturnValid = false;
            _factsNavigationReturnFrame = 0;
            _factsNavigationReturnAgentEntityId = 0;
            _factsNavigationReturnRecordGeneration = 0L;
            _factsNavigationReturnSessionGeneration = 0L;
            _factsPresentationRevision++;
        }

        internal bool TryResolveFactsBehaviorSelection(
            int entryIndex,
            out CharacterTestRuntimeAiLabLogicElementKind kind,
            out int index)
        {
            kind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            index = -1;
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out _)
                || card?.LogicPayload == null)
            {
                return false;
            }

            if (!TryResolveFactsBehaviorNavigationTarget(out string target))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic = card.LogicPayload;
            for (int i = 0; i < logic.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = logic.Stages[i];
                if (MatchesFactsCandidate(target, "/stage/", stage.StageId)
                    || MatchesFactsStageAction(target, stage))
                {
                    kind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
                    index = i;
                    return true;
                }
            }
            for (int i = 0; i < logic.Detectors.Count; i++)
            {
                if (MatchesFactsCandidate(target, "/detector/", logic.Detectors[i].DetectorId))
                {
                    kind = CharacterTestRuntimeAiLabLogicElementKind.Detector;
                    index = i;
                    return true;
                }
            }
            for (int i = 0; i < logic.Transitions.Count; i++)
            {
                if (MatchesFactsCandidate(
                        target,
                        "/transition/",
                        logic.Transitions[i].TransitionId))
                {
                    kind = CharacterTestRuntimeAiLabLogicElementKind.Transition;
                    index = i;
                    return true;
                }
            }
            return false;
        }

        internal bool TryResolveFactsBehaviorNavigationTarget(out string target)
        {
            target = string.Empty;
            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt = _frameSelection.CommittedReceipt;
            if (!receipt.IsValid
                || _timeline == null
                || !_timeline.TryGetDecisionTrace(
                    receipt.Bundle.Record,
                    receipt.ResolvedAgentEntityId,
                    out var trace))
            {
                return false;
            }
            return CharacterTestRuntimeAiLabFactsBehaviorTargetResolver.TryResolve(
                trace,
                _factsSelectedStableId,
                out target);
        }

        internal bool TryResolveFactsTimelineLocator(
            out CharacterTestRuntimeAiLabTimelineItemLocator locator)
        {
            locator = default;
            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt = _frameSelection.CommittedReceipt;
            if (!receipt.IsValid)
                return false;
            TryResolveFactsBehaviorNavigationTarget(out string behaviorTarget);
            CharacterTestRuntimeAiLabTimelineProjection projection = BuildTimelineProjection();
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items = projection.Tracks[trackIndex].Items;
                for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item = items[itemIndex];
                    if (TryMatchFactsTimelineItem(item, receipt, behaviorTarget, out locator))
                        return true;
                    for (int memberIndex = 0; memberIndex < item.Members.Count; memberIndex++)
                    {
                        if (TryMatchFactsTimelineItem(item.Members[memberIndex], receipt, behaviorTarget, out locator))
                            return true;
                    }
                }
            }
            return false;
        }

        private bool TryMatchFactsTimelineItem(
            CharacterTestRuntimeAiLabTimelineItem item,
            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt,
            string behaviorTarget,
            out CharacterTestRuntimeAiLabTimelineItemLocator locator)
        {
            locator = default;
            if (item == null
                || item.Provenance == CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate
                || receipt.Frame < item.StartFrame
                || receipt.Frame > item.EndFrame
                || (receipt.ResolvedAgentEntityId > 0
                    && item.AgentEntityId > 0
                    && item.AgentEntityId != receipt.ResolvedAgentEntityId))
            {
                return false;
            }
            bool matchesSelected = !string.IsNullOrEmpty(_factsSelectedStableId)
                && (string.Equals(item.SourceTraceId, _factsSelectedStableId, StringComparison.Ordinal)
                    || string.Equals(item.StableKey, _factsSelectedStableId, StringComparison.Ordinal));
            bool matchesBehavior = !string.IsNullOrEmpty(behaviorTarget)
                && (string.Equals(item.BehaviorStepId, behaviorTarget, StringComparison.Ordinal)
                    || string.Equals(item.BehaviorEntryId, behaviorTarget, StringComparison.Ordinal)
                    || behaviorTarget.EndsWith("/" + item.BehaviorStepId, StringComparison.Ordinal));
            if (!matchesSelected && !matchesBehavior)
                return false;
            locator = item.CreateLocator();
            return locator.IsValid;
        }

        private static bool MatchesFactsCandidate(string candidate, string separator, string stableId)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(stableId))
                return false;
            int marker = candidate.LastIndexOf(separator, StringComparison.Ordinal);
            return marker >= 0
                ? string.Equals(candidate.Substring(marker + separator.Length), stableId, StringComparison.Ordinal)
                : string.Equals(candidate, stableId, StringComparison.Ordinal);
        }

        private static bool MatchesFactsStageAction(
            string candidate,
            CharacterTestRuntimeAiLabLogicStage stage)
        {
            if (string.IsNullOrEmpty(candidate) || stage == null || !stage.OwnsPrimaryAction)
                return false;
            switch (stage.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    return string.Equals(candidate, stage.CharacterActionId.ToString(), StringComparison.Ordinal);
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    return string.Equals(
                        candidate,
                        "ability:" + stage.AbilityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal);
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    return string.Equals(candidate, "action:move-to-target", StringComparison.Ordinal);
                default:
                    return false;
            }
        }
    }

    internal static class CharacterTestRuntimeAiLabFactsBehaviorTargetResolver
    {
        internal static bool TryResolve(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string selectedStableId,
            out string targetStableId)
        {
            targetStableId = string.Empty;
            if (string.IsNullOrEmpty(selectedStableId))
                return false;

            for (int candidateIndex = 0; candidateIndex < trace.Candidates.Count; candidateIndex++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[candidateIndex];
                if (string.Equals(candidate.StableId, selectedStableId, StringComparison.Ordinal))
                    return TryUseCandidate(candidate, out targetStableId);
            }

            bool factExists = false;
            for (int factIndex = 0; factIndex < trace.Facts.Count; factIndex++)
            {
                if (string.Equals(trace.Facts[factIndex].StableId, selectedStableId, StringComparison.Ordinal))
                {
                    factExists = true;
                    break;
                }
            }
            if (!factExists)
                return false;

            string fallback = string.Empty;
            for (int candidateIndex = 0; candidateIndex < trace.Candidates.Count; candidateIndex++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[candidateIndex];
                if (!IsBehaviorCandidate(candidate))
                    continue;
                for (int link = 0; link < candidate.RelatedFactCount; link++)
                {
                    if (!string.Equals(candidate.GetRelatedFactStableId(link), selectedStableId, StringComparison.Ordinal))
                        continue;
                    if (candidate.State == CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected)
                    {
                        targetStableId = candidate.StableId;
                        return true;
                    }
                    if (fallback.Length == 0)
                        fallback = candidate.StableId;
                }
            }
            targetStableId = fallback;
            return targetStableId.Length > 0;
        }

        private static bool TryUseCandidate(
            CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate,
            out string targetStableId)
        {
            targetStableId = IsBehaviorCandidate(candidate)
                ? candidate.StableId ?? string.Empty
                : string.Empty;
            return targetStableId.Length > 0;
        }

        private static bool IsBehaviorCandidate(
            CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate)
        {
            if (candidate.Layer != CharacterTestRuntimeAiLabDecisionTraceLayer.LogicCondition
                && candidate.Layer != CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction)
            {
                return false;
            }
            string stableId = candidate.StableId;
            return !string.IsNullOrEmpty(stableId)
                && (stableId.IndexOf("/stage/", StringComparison.Ordinal) >= 0
                    || stableId.IndexOf("/detector/", StringComparison.Ordinal) >= 0
                    || stableId.IndexOf("/transition/", StringComparison.Ordinal) >= 0);
        }
    }
}
