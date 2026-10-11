using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiPlannerEvaluator
    {
        private const int PlannerFactCount = 21;
        private const string PlannerFactSource = "character-test.runtime-ai-planner.sense";
        private const string FallbackTargetMissing = "planner.fallback.target-missing";
        private const string FallbackNoAction = "planner.fallback.no-executable-action";

        private List<PlannerDecisionTraceState> _decisionTraceStates;
        private string[] _decisionTraceActionStableIds;

        private void InitializeDecisionTraceCapture()
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerCompiledAction> actions = _profile.Actions;
            _decisionTraceStates = new List<PlannerDecisionTraceState>(AgentInitialCapacity);
            _decisionTraceActionStableIds = new string[actions.Count];
            for (int i = 0; i < actions.Count; i++)
                _decisionTraceActionStableIds[i] = "planner.action." + actions[i].ActionId;
        }

        private void BeginDecisionTraceCapture(
            int entityId,
            int frame,
            in CharacterTestRuntimeAiPlannerDecision previousDecision)
        {
            PlannerDecisionTraceState trace = GetOrCreateDecisionTraceState(entityId);
            trace.BeginFrame(frame, BuildCommittedState(previousDecision));
        }

        private void CaptureDecisionTraceFacts(
            int entityId,
            CharacterTestRuntimeAiPlannerFactState facts,
            int frame)
        {
            PlannerDecisionTraceState trace = FindDecisionTraceState(entityId);
            trace?.CaptureFacts(facts, frame);
            trace?.CaptureRuntimeEffect(facts.SelfActionRunning, frame);
        }

        private bool EvaluateActionConditionsForTrace(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            int entityId,
            in CharacterTestRuntimeAiPlannerCompiledAction action,
            CharacterTestRuntimeAiPlannerFactState facts,
            CharacterTestRuntimeAiJointDirective jointDirective = default)
        {
            PlannerDecisionTraceState trace = FindDecisionTraceState(entityId);
            if (trace != null)
                trace.PendingFactMask = 0UL;
            if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack
                && facts.SelfActionRunning)
            {
                return false;
            }
            bool usesOpportunity = (action.Kind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack
                    || action.Kind == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget)
                && (jointDirective.Evaluated
                    ? jointDirective.HasSelection && !jointDirective.IsCompatibility
                    : _primaryAttackEligibility != null)
                && !facts.SelfActionRunning;
            bool opportunityEvaluated = false;
            CharacterTestAttackOpportunityResult opportunity =
                default(CharacterTestAttackOpportunityResult);
            for (int i = 0; i < action.ConditionCount; i++)
            {
                CharacterTestRuntimeAiPlannerConditionDefinition condition = action.GetCondition(i);
                if (trace != null)
                    trace.PendingFactMask |= PlannerFactCatalog.Mask(condition.Fact);

                // TargetInAttackRange remains a compatibility fact for
                // diagnostics and non-PrimaryAttack actions. For a real
                // PrimaryAttack candidate, the M1 opportunity result is the
                // final eligibility authority and may be valid even when the
                // old center-distance fact is false.
                if (usesOpportunity
                    && condition.Fact == CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange)
                {
                    if (!opportunityEvaluated)
                    {
                        opportunity = jointDirective.Evaluated
                            ? jointDirective.Opportunity
                            : _primaryAttackEligibility(
                                entity,
                                facts.TargetEntityId,
                                frame);
                        opportunityEvaluated = true;
                    }

                    if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack
                        && condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals
                        && condition.BoolValue)
                    {
                        if (!IsPrimaryAttackSelectionOpportunity(opportunity))
                            return false;
                        continue;
                    }

                    if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget
                        && condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals
                        && !condition.BoolValue)
                    {
                        // The approach branch is M1-owned when its
                        // opportunity is available. Current/Turn must not
                        // fall back to the legacy center-distance fact, and
                        // stale/unavailable stays fail-closed.
                        if (!IsTranslationSelectionOpportunity(opportunity))
                            return false;
                        continue;
                    }
                }

                if (!CharacterTestRuntimeAiPlannerConditionEvaluator.IsSatisfied(facts, condition))
                    return false;
            }

            if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack
                && (jointDirective.Evaluated
                    ? jointDirective.HasSelection && !jointDirective.IsCompatibility
                    : _primaryAttackEligibility != null)
                && !facts.SelfActionRunning
                && (!opportunityEvaluated
                    ? !IsPrimaryAttackSelectionOpportunity(
                        jointDirective.Evaluated
                            ? jointDirective.Opportunity
                            : _primaryAttackEligibility(
                                entity,
                                facts.TargetEntityId,
                                frame))
                    : !IsPrimaryAttackSelectionOpportunity(opportunity)))
            {
                return false;
            }
            return true;
        }

        private void RecordDecisionTraceActionCandidate(
            int entityId,
            in CharacterTestRuntimeAiPlannerCompiledAction action,
            int stableRank,
            bool inActivePath,
            bool executable)
        {
            PlannerDecisionTraceState trace = FindDecisionTraceState(entityId);
            if (trace == null)
                return;

            var candidate = new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                Identity = CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.PlannerActionKind, action.ActionId),
                StableId = ResolveActionStableId(action.ActionId),
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction,
                State = !inActivePath
                    ? CharacterTestRuntimeAiLabDecisionTraceCandidateState.Suppressed
                    : executable
                        ? CharacterTestRuntimeAiLabDecisionTraceCandidateState.Evaluated
                        : CharacterTestRuntimeAiLabDecisionTraceCandidateState.Ineligible,
                Score = executable ? action.Score : 0f,
                ScoreAvailable = executable,
                Priority = 0,
                PriorityAvailable = false,
                Cost = executable ? action.Cost : 0f,
                CostAvailable = executable,
                StableRank = stableRank,
                ReasonCode = !inActivePath
                    ? CharacterTestAiTraceReason.PLANNER_ACTION_OUTSIDE_ACTIVE_PATH
                    : executable
                        ? CharacterTestAiTraceReason.PLANNER_ACTION_EVALUATED
                        : CharacterTestAiTraceReason.PLANNER_ACTION_CONDITION_FAILED
            };
            if (inActivePath)
            {
                trace.MainActionFactReadMask |= trace.PendingFactMask;
                AddCandidateFactLinks(ref candidate, trace.PendingFactMask, trace);
            }
            trace.AddCandidate(candidate);
        }

        private void CommitDecisionTraceSelection(
            int entityId,
            in CharacterTestRuntimeAiPlannerDecision decision,
            int frame)
        {
            PlannerDecisionTraceState trace = FindDecisionTraceState(entityId);
            if (trace == null)
                return;
            string selectedStableId = ResolveActionStableId(decision.SelectedActionId);
            trace.MarkSelected(selectedStableId);
            trace.Commit = BuildCommit(
                trace.PreviousCommitted,
                decision,
                frame,
                CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed,
                CharacterTestAiTraceReason.PLANNER_ACTION_SELECTED);
            trace.ApplyRuntimeEffectToCommit();
        }

        private void RecordDecisionTraceFallback(
            int entityId,
            in CharacterTestRuntimeAiPlannerDecision decision,
            int frame,
            bool targetMissing)
        {
            PlannerDecisionTraceState trace = FindDecisionTraceState(entityId);
            if (trace == null)
                return;
            trace.AddCandidate(new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                Identity = new RuntimeTraceIdentity(CharacterTestAiEvidenceIds.PlannerFallbackKind, targetMissing ? 1 : 2),
                StableId = targetMissing ? FallbackTargetMissing : FallbackNoAction,
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.Fallback,
                State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected,
                StableRank = 0,
                ReasonCode = targetMissing ? CharacterTestAiTraceReason.PLANNER_TARGET_MISSING : CharacterTestAiTraceReason.PLANNER_NO_EXECUTABLE_ACTION
            });
            trace.Commit = BuildCommit(
                trace.PreviousCommitted,
                decision,
                frame,
                CharacterTestRuntimeAiLabPlannerLogicCommitKind.Fallback,
                targetMissing ? CharacterTestAiTraceReason.PLANNER_TARGET_MISSING : CharacterTestAiTraceReason.PLANNER_NO_EXECUTABLE_ACTION);
            trace.ApplyRuntimeEffectToCommit();
        }

        private void RecordDecisionTraceCommandResult(
            int entityId,
            RuntimeCommandValidationResult result)
        {
            PlannerDecisionTraceState trace = FindDecisionTraceState(entityId);
            if (trace == null)
                return;
            CharacterTestRuntimeAiLabCommitTrace commit = trace.Commit;
            commit.CommandEnqueueKind = result.Success
                ? CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued
                : CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected;
            commit.CommandEvidenceFrame = trace.Frame;
            commit.CommandReasonCode = result.Success ? 0 : CharacterTestAiTraceReason.COMMAND_REJECTED;
            trace.Commit = commit;
        }

        internal void WriteDecisionTraces(
            CharacterTestRuntimeAiLabDecisionTraceWriter writer,
            int frame)
        {
            if (!writer.IsValid)
                return;
            for (int i = 0; i < _decisionTraceStates.Count; i++)
            {
                PlannerDecisionTraceState trace = _decisionTraceStates[i];
                if (trace.Frame != frame)
                    continue;
                CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent = writer.BeginAgent(trace.EntityId);
                if (!agent.IsValid)
                    continue;
                agent.SetPrevious(trace.PreviousCommitted);
                for (int factIndex = 0; factIndex < PlannerFactCount; factIndex++)
                    agent.AddFact(trace.BuildFact(factIndex));
                for (int candidateIndex = 0; candidateIndex < trace.CandidateCount; candidateIndex++)
                    agent.AddCandidate(trace.Candidates[candidateIndex]);
                if (trace.CandidateFactLinkOverflow)
                    agent.RecordCandidateFactLinkOverflow();
                agent.SetCommit(trace.Commit);
            }
        }

        /// <remarks>NoAlloc. The behavior-card overlay owns the single main-action authority for this frame.</remarks>
        internal void SuppressDecisionTraceSelectionForBehaviorOverlay(int entityId)
        {
            PlannerDecisionTraceState trace = FindDecisionTraceState(entityId);
            if (trace == null)
                return;
            for (int i = 0; i < trace.CandidateCount; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[i];
                if (candidate.Layer != CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction
                    || candidate.State != CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected)
                {
                    continue;
                }
                candidate.State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Suppressed;
                candidate.ReasonCode = CharacterTestAiTraceReason.PLANNER_ACTION_SUPPRESSED_BY_BEHAVIOR_CARD;
                trace.Candidates[i] = candidate;
            }
        }

        internal void RestoreDecisionTraceHistory(
            int entityId,
            in CharacterTestRuntimeAiLabDecisionTraceAgentView source)
        {
            PlannerDecisionTraceState trace = GetOrCreateDecisionTraceState(entityId);
            trace.RestoreFactHistory(source.Facts);
        }

        private PlannerDecisionTraceState GetOrCreateDecisionTraceState(int entityId)
        {
            PlannerDecisionTraceState trace = FindDecisionTraceState(entityId);
            if (trace != null)
                return trace;
            trace = new PlannerDecisionTraceState(
                entityId,
                Math.Max(4, _profile.Actions.Count + 2));
            _decisionTraceStates.Add(trace);
            return trace;
        }

        private PlannerDecisionTraceState FindDecisionTraceState(int entityId)
        {
            for (int i = 0; i < _decisionTraceStates.Count; i++)
            {
                if (_decisionTraceStates[i].EntityId == entityId)
                    return _decisionTraceStates[i];
            }
            return null;
        }

        private string ResolveActionStableId(int actionId)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerCompiledAction> actions = _profile.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].ActionId == actionId)
                    return _decisionTraceActionStableIds[i];
            }
            return string.Empty;
        }

        private CharacterTestRuntimeAiLabCommittedStateTrace BuildCommittedState(
            in CharacterTestRuntimeAiPlannerDecision decision)
        {
            return new CharacterTestRuntimeAiLabCommittedStateTrace
            {
                StageIdentity = CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.PlannerStateKind, decision.ActiveStateId),
                MainActionIdentity = CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.PlannerActionKind, decision.SelectedActionId),
                StageStableId = decision.ActiveStatePath,
                MainActionStableId = ResolveActionStableId(decision.SelectedActionId)
            };
        }

        private CharacterTestRuntimeAiLabCommitTrace BuildCommit(
            in CharacterTestRuntimeAiLabCommittedStateTrace previous,
            in CharacterTestRuntimeAiPlannerDecision decision,
            int frame,
            CharacterTestRuntimeAiLabPlannerLogicCommitKind kind,
            int reasonCode)
        {
            string actionStableId = ResolveActionStableId(decision.SelectedActionId);
            if (kind == CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed
                && previous.StageIdentity.SourceId == decision.ActiveStateId
                && string.Equals(previous.MainActionStableId, actionStableId, StringComparison.Ordinal))
            {
                kind = CharacterTestRuntimeAiLabPlannerLogicCommitKind.Held;
                reasonCode = CharacterTestAiTraceReason.PLANNER_SELECTION_HELD;
            }
            return new CharacterTestRuntimeAiLabCommitTrace
            {
                StageIdentity = CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.PlannerStateKind, decision.ActiveStateId),
                MainActionIdentity = CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.PlannerActionKind, decision.SelectedActionId),
                PlannerLogicKind = kind,
                CommandEnqueueKind = CharacterTestRuntimeAiLabCommandEnqueueKind.NotRequested,
                RuntimeEffectKind = CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved,
                EvidenceFrame = frame,
                PlannerEvidenceFrame = frame,
                CommandEvidenceFrame = -1,
                RuntimeEffectEvidenceFrame = -1,
                StageStableId = decision.ActiveStatePath,
                MainActionStableId = actionStableId,
                CommandKind = decision.ShouldSubmitControlIntent
                    ? CharacterTestRuntimeAiLabDecisionTraceCommandKind.SetControlIntent
                    : CharacterTestRuntimeAiLabDecisionTraceCommandKind.None,
                CommandActionId = decision.SelectedActionId,
                ReasonCode = reasonCode,
                PlannerReasonCode = reasonCode,
                CommandReasonCode = 0,
                RuntimeEffectReasonCode = 0
            };
        }

        private static void AddCandidateFactLinks(
            ref CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate,
            ulong mask,
            PlannerDecisionTraceState trace)
        {
            for (int i = 0; i < PlannerFactCount; i++)
            {
                if ((mask & (1UL << i)) == 0UL)
                    continue;
                if (!candidate.TryAddRelatedFact(PlannerFactCatalog.StableId((CharacterTestRuntimeAiPlannerFactKind)i), CharacterTestAiEvidenceIds.PlannerFact((CharacterTestRuntimeAiPlannerFactKind)i)))
                    trace.CandidateFactLinkOverflow = true;
            }
        }

        private sealed class PlannerDecisionTraceState
        {
            private readonly FactHistory[] _facts = new FactHistory[PlannerFactCount];

            public PlannerDecisionTraceState(int entityId, int candidateCapacity)
            {
                EntityId = entityId;
                Candidates = new CharacterTestRuntimeAiLabCandidateEvaluationTrace[candidateCapacity];
            }

            public int EntityId { get; }
            public int Frame { get; private set; } = -1;
            public CharacterTestRuntimeAiLabCommittedStateTrace PreviousCommitted;
            public CharacterTestRuntimeAiLabCommitTrace Commit;
            public CharacterTestRuntimeAiLabCandidateEvaluationTrace[] Candidates { get; }
            public int CandidateCount;
            public ulong PendingFactMask;
            public ulong MainActionFactReadMask;
            public bool CandidateFactLinkOverflow;
            private bool _hasActionLifecycleBaseline;
            private bool _previousActionRunning;
            private CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind _runtimeEffectKind;
            private int _runtimeEffectFrame = -1;
            private string _runtimeEffectActionStableId = string.Empty;
            private RuntimeTraceIdentity _runtimeEffectActionIdentity;

            public void BeginFrame(int frame, in CharacterTestRuntimeAiLabCommittedStateTrace previous)
            {
                Frame = frame;
                PreviousCommitted = previous;
                Commit = default;
                CandidateCount = 0;
                PendingFactMask = 0UL;
                MainActionFactReadMask = 0UL;
                CandidateFactLinkOverflow = false;
                _runtimeEffectKind = CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved;
                _runtimeEffectFrame = -1;
                _runtimeEffectActionStableId = string.Empty;
                _runtimeEffectActionIdentity = default;
            }

            public void CaptureRuntimeEffect(bool actionRunning, int frame)
            {
                if (!_hasActionLifecycleBaseline)
                {
                    _hasActionLifecycleBaseline = true;
                    _previousActionRunning = actionRunning;
                    if (!actionRunning)
                        return;
                    _runtimeEffectKind = CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running;
                }
                else if (actionRunning && !_previousActionRunning)
                    _runtimeEffectKind = CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Started;
                else if (actionRunning)
                    _runtimeEffectKind = CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running;
                else if (_previousActionRunning)
                    _runtimeEffectKind = CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Completed;
                _previousActionRunning = actionRunning;
                if (_runtimeEffectKind != CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved)
                {
                    _runtimeEffectFrame = frame;
                    _runtimeEffectActionStableId = PreviousCommitted.MainActionStableId ?? string.Empty;
                    _runtimeEffectActionIdentity = PreviousCommitted.MainActionIdentity;
                }
            }

            public void ApplyRuntimeEffectToCommit()
            {
                if (_runtimeEffectKind == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved)
                    return;
                CharacterTestRuntimeAiLabCommitTrace commit = Commit;
                commit.RuntimeEffectKind = _runtimeEffectKind;
                commit.RuntimeEffectEvidenceFrame = _runtimeEffectFrame;
                commit.RuntimeEffectActionStableId = _runtimeEffectActionStableId;
                commit.RuntimeEffectActionIdentity = _runtimeEffectActionIdentity;
                commit.RuntimeEffectReasonCode = _runtimeEffectKind == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Started
                    ? CharacterTestAiTraceReason.PLANNER_RUNTIME_ACTION_STARTED
                    : _runtimeEffectKind == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running
                        ? CharacterTestAiTraceReason.PLANNER_RUNTIME_ACTION_RUNNING
                        : CharacterTestAiTraceReason.PLANNER_RUNTIME_ACTION_COMPLETED;
                Commit = commit;
            }

            public void RestoreFactHistory(
                IReadOnlyList<CharacterTestRuntimeAiLabFactObservationTrace> source)
            {
                for (int i = 0; i < PlannerFactCount; i++)
                    _facts[i] = default;
                for (int i = 0; source != null && i < source.Count; i++)
                {
                    CharacterTestRuntimeAiLabFactObservationTrace fact = source[i];
                    if (fact.Domain != CharacterTestRuntimeAiLabDecisionTraceFactDomain.RuntimeAiPlanner)
                        continue;
                    int index = (int)fact.PlannerFact;
                    if (index < 0 || index >= PlannerFactCount)
                        continue;
                    _facts[index] = new FactHistory
                    {
                        Value = fact.Value,
                        PreviousValue = fact.PreviousValue,
                        HasValue = true,
                        HasPrevious = !fact.BaselineUnknown,
                        LastChangedFrame = fact.LastChangedFrame
                    };
                    if (fact.PlannerFact == CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning
                        && fact.Value.Kind == CharacterTestRuntimeAiLabDecisionTraceValueKind.Boolean)
                    {
                        _hasActionLifecycleBaseline = true;
                        _previousActionRunning = fact.Value.Boolean;
                    }
                }
            }

            public void CaptureFacts(CharacterTestRuntimeAiPlannerFactState facts, int frame)
            {
                for (int i = 0; i < PlannerFactCount; i++)
                {
                    CharacterTestRuntimeAiLabDecisionTraceValue value =
                        PlannerFactCatalog.ReadValue((CharacterTestRuntimeAiPlannerFactKind)i, facts);
                    FactHistory history = _facts[i];
                    if (!history.HasValue || !history.Value.Equals(value))
                        history.LastChangedFrame = frame;
                    history.PreviousValue = history.Value;
                    history.HasPrevious = history.HasValue;
                    history.Value = value;
                    history.HasValue = true;
                    _facts[i] = history;
                }
            }

            public CharacterTestRuntimeAiLabFactObservationTrace BuildFact(int index)
            {
                CharacterTestRuntimeAiPlannerFactKind kind = (CharacterTestRuntimeAiPlannerFactKind)index;
                FactHistory history = _facts[index];
                bool participated = (MainActionFactReadMask & (1UL << index)) != 0UL;
                return new CharacterTestRuntimeAiLabFactObservationTrace
                {
                    Identity = CharacterTestAiEvidenceIds.PlannerFact(kind),
                    SourceIdentity = new RuntimeTraceIdentity(CharacterTestAiEvidenceIds.PlannerSenseKind),
                    StableId = PlannerFactCatalog.StableId(kind),
                    Domain = CharacterTestRuntimeAiLabDecisionTraceFactDomain.RuntimeAiPlanner,
                    PlannerFact = kind,
                    Value = history.Value,
                    PreviousValue = history.PreviousValue,
                    SourceStableId = PlannerFactSource,
                    ObservedFrame = Frame,
                    SourceFrame = -1,
                    SourceGeneration = 0L,
                    LastChangedFrame = history.LastChangedFrame,
                    AuthorityKind = CharacterTestRuntimeAiLabDecisionTraceAuthorityKind.Derived,
                    ParticipatedLayers = participated
                        ? CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction
                        : CharacterTestRuntimeAiLabDecisionTraceLayer.None,
                    BaselineUnknown = !history.HasPrevious,
                    SourceUnknown = true,
                    DerivationCode = CharacterTestAiTraceReason.FACT_DERIVE_PLANNER_SENSE
                };
            }

            public void AddCandidate(in CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate)
            {
                if (CandidateCount < Candidates.Length)
                    Candidates[CandidateCount++] = candidate;
            }

            public void MarkSelected(string stableId)
            {
                for (int i = 0; i < CandidateCount; i++)
                {
                    if (!string.Equals(Candidates[i].StableId, stableId, StringComparison.Ordinal))
                        continue;
                    CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = Candidates[i];
                    candidate.State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected;
                    candidate.ReasonCode = CharacterTestAiTraceReason.PLANNER_ACTION_SELECTED;
                    Candidates[i] = candidate;
                    return;
                }
            }
        }

        private struct FactHistory
        {
            public CharacterTestRuntimeAiLabDecisionTraceValue Value;
            public CharacterTestRuntimeAiLabDecisionTraceValue PreviousValue;
            public int LastChangedFrame;
            public bool HasValue;
            public bool HasPrevious;
        }

        private static class PlannerFactCatalog
        {
            private static readonly string[] StableIds =
            {
                "planner.fact.target-exists",
                "planner.fact.target-in-attack-range",
                "planner.fact.target-distance",
                "planner.fact.self-action-running",
                "planner.fact.move-path-available",
                "planner.fact.target-visible",
                "planner.fact.target-line-of-sight-blocked",
                "planner.fact.target-last-seen-age-frames",
                "planner.fact.target-threat-score",
                "planner.fact.self-surface-kind",
                "planner.fact.self-on-hard-slick",
                "planner.fact.self-near-movable-object",
                "planner.fact.move-direct-path-clear",
                "planner.fact.move-candidate-index",
                "planner.fact.environment-near-obstacle",
                "planner.fact.environment-near-hazard",
                "planner.fact.environment-near-useful-object",
                "planner.fact.target-attack-will-hit-self",
                "planner.fact.target-attack-impact-frames",
                "planner.fact.target-attack-instance-responded",
                "planner.fact.self-dodge-cooldown-ready"
            };

            public static string StableId(CharacterTestRuntimeAiPlannerFactKind kind)
            {
                int index = (int)kind;
                return index >= 0 && index < StableIds.Length ? StableIds[index] : string.Empty;
            }

            public static ulong Mask(CharacterTestRuntimeAiPlannerFactKind kind)
            {
                int index = (int)kind;
                return index >= 0 && index < 64 ? 1UL << index : 0UL;
            }

            public static CharacterTestRuntimeAiLabDecisionTraceValue ReadValue(
                CharacterTestRuntimeAiPlannerFactKind kind,
                CharacterTestRuntimeAiPlannerFactState facts)
            {
                if (facts == null)
                    return CharacterTestRuntimeAiLabDecisionTraceValue.Unavailable;
                switch (kind)
                {
                    case CharacterTestRuntimeAiPlannerFactKind.TargetExists: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetExists);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetInAttackRange);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetDistance: return CharacterTestRuntimeAiLabDecisionTraceValue.FromFloat(facts.TargetDistance);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetDistanceAvailable: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetDistanceAvailable);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetRootDistance: return CharacterTestRuntimeAiLabDecisionTraceValue.FromFloat(facts.TargetRootDistance);
                    case CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.SelfActionRunning);
                    case CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.MovePathAvailable);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetVisible: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetVisible);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetDead: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetDead);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetLifeState: return CharacterTestRuntimeAiLabDecisionTraceValue.FromInteger((int)facts.TargetLifeState);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetLineOfSightBlocked);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames: return CharacterTestRuntimeAiLabDecisionTraceValue.FromInteger(facts.TargetLastSeenAgeFrames);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore: return CharacterTestRuntimeAiLabDecisionTraceValue.FromFloat(facts.TargetThreatScore);
                    case CharacterTestRuntimeAiPlannerFactKind.SelfSurfaceKind: return CharacterTestRuntimeAiLabDecisionTraceValue.FromInteger(facts.SelfSurfaceKind);
                    case CharacterTestRuntimeAiPlannerFactKind.SelfOnHardSlick: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.SelfOnHardSlick);
                    case CharacterTestRuntimeAiPlannerFactKind.SelfNearMovableObject: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.SelfNearMovableObject);
                    case CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.DirectPathClear);
                    case CharacterTestRuntimeAiPlannerFactKind.MoveCandidateIndex: return CharacterTestRuntimeAiLabDecisionTraceValue.FromInteger(facts.NavigationCandidateIndex);
                    case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.EnvironmentNearObstacle);
                    case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.EnvironmentNearHazard);
                    case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.EnvironmentNearUsefulObject);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetAttackWillHitSelf: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetAttackWillHitSelf);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetAttackImpactFrames: return CharacterTestRuntimeAiLabDecisionTraceValue.FromInteger(facts.TargetAttackImpactFrames);
                    case CharacterTestRuntimeAiPlannerFactKind.TargetAttackInstanceResponded: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetAttackInstanceResponded);
                    case CharacterTestRuntimeAiPlannerFactKind.SelfDodgeCooldownReady: return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.SelfDodgeCooldownReady);
                    default: return CharacterTestRuntimeAiLabDecisionTraceValue.Unavailable;
                }
            }
        }
    }
}
