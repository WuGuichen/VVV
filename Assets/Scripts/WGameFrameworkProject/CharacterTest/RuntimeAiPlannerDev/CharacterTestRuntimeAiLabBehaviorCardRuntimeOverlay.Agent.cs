using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay
    {
        private readonly struct Binding
        {
            public Binding(
                string entryId,
                int numericId,
                int order,
                float score,
                bool enabled,
                CharacterTestRuntimeAiLabCompiledLogicProgram program,
                CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason switchReason,
                CharacterTestBehaviorCardSelectionDefinition selectionDefinition, TacticalCard.TacticalAutomaticExecutionBinding automatic = null)
            {
                if (enabled && program != null && numericId <= 0) throw new ArgumentOutOfRangeException(nameof(numericId), "Behavior entries require authoring-assigned numeric identities.");
                NumericId = numericId;
                EntryId = entryId ?? string.Empty;
                Order = order;
                Score = score;
                Enabled = enabled;
                Program = program;
                SwitchReason = switchReason;
                SelectionDefinition = selectionDefinition; Automatic = automatic;
            }

            internal TacticalCard.TacticalAutomaticExecutionBinding Automatic { get; }
            public string EntryId { get; }
            public int NumericId { get; }
            public int Order { get; }
            public float Score { get; }
            public bool Enabled { get; }
            public CharacterTestRuntimeAiLabCompiledLogicProgram Program { get; }
            public CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason SwitchReason { get; }
            public CharacterTestBehaviorCardSelectionDefinition SelectionDefinition { get; }
        }

        private sealed class AgentInstance
        {
            private readonly IReadOnlyList<Binding> _bindings;
            private readonly BindingSwitchCoordinator _bindingSwitch = new BindingSwitchCoordinator();
            private readonly DecisionTraceState _decisionTrace;

            private bool[] _bindingEvaluated => _decisionTrace.BindingEvaluated;
            private bool[] _bindingEligible => _decisionTrace.BindingEligible;
            private DecisionTraceFactHistory[] _factHistory => _decisionTrace.FactHistory;
            private CharacterTestRuntimeAiLabCommittedStateTrace _previousCommitted
            {
                get => _decisionTrace.PreviousCommitted;
                set => _decisionTrace.PreviousCommitted = value;
            }
            private CharacterTestRuntimeAiLabCommandEnqueueKind _commandEnqueueKind
            {
                get => _decisionTrace.CommandEnqueueKind;
                set => _decisionTrace.CommandEnqueueKind = value;
            }
            private int _commandReasonCode
            {
                get => _decisionTrace.CommandReasonCode;
                set => _decisionTrace.CommandReasonCode = value;
            }
            private int _traceFrame
            {
                get => _decisionTrace.TraceFrame;
                set => _decisionTrace.TraceFrame = value;
            }
            private bool _hasTerminalCommit
            {
                get => _decisionTrace.HasTerminalCommit;
                set => _decisionTrace.HasTerminalCommit = value;
            }
            private CharacterTestRuntimeAiLabCommittedStateTrace _terminalCommitted
            {
                get => _decisionTrace.TerminalCommitted;
                set => _decisionTrace.TerminalCommitted = value;
            }
            private CharacterTestRuntimeAiLabPlannerLogicCommitKind _terminalLogicKind
            {
                get => _decisionTrace.TerminalLogicKind;
                set => _decisionTrace.TerminalLogicKind = value;
            }
            private CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind _terminalRuntimeEffectKind
            {
                get => _decisionTrace.TerminalRuntimeEffectKind;
                set => _decisionTrace.TerminalRuntimeEffectKind = value;
            }
            private int _terminalRuntimeEffectReasonCode
            {
                get => _decisionTrace.TerminalRuntimeEffectReasonCode;
                set => _decisionTrace.TerminalRuntimeEffectReasonCode = value;
            }
            private CharacterTestRuntimeAiLabRuntimeIntentKind _terminalIntentKind
            {
                get => _decisionTrace.TerminalIntentKind;
                set => _decisionTrace.TerminalIntentKind = value;
            }

            public AgentInstance(
                int entityId,
                IReadOnlyList<Binding> bindings,
                IReadOnlyList<CharacterTestCharacterEntity> entities,
                ICharacterTestAiNavigationService navigationService,
                CharacterTestPerceptionFrameCache perceptionFrameCache,
                CharacterTestCharacterRuntimeModule characterRuntime = null, CharacterTestRuntimeAiPlannerAttackOpportunityAdapter attackOpportunity = null)
            {
                EntityId = entityId;
                _bindings = bindings;
                _decisionTrace = new DecisionTraceState(bindings.Count);
                Runtimes = new RuntimeBridge[bindings.Count];
                Evaluators = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator[bindings.Count];
                for (int i = 0; i < bindings.Count; i++)
                {
                    Runtimes[i] = new RuntimeBridge(
                        entityId,
                        bindings[i].EntryId,
                        bindings[i].Program,
                        entities,
                        navigationService,
                        perceptionFrameCache, bindings[i].Automatic != null ? null : characterRuntime, attackOpportunity, bindings[i].NumericId);
                    Evaluators[i] = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(
                        bindings[i].Program,
                        Runtimes[i]);
                }
            }

            internal ulong AutomaticExecutionId { get; set; }
            public int EntityId { get; }
            internal bool HasTerminalCommit => _hasTerminalCommit;
            public RuntimeFrame CurrentFrame { get; private set; }
            public CharacterTestRuntimeAiPlannerFactState CurrentFacts { get; private set; }
            public int ActiveBindingIndex
            {
                get => _bindingSwitch.ActiveBindingIndex;
                set => _bindingSwitch.ActiveBindingIndex = value;
            }
            public int CommandResultBindingIndex
            {
                get => _bindingSwitch.CommandResultBindingIndex;
                set => _bindingSwitch.CommandResultBindingIndex = value;
            }
            public int PendingBindingIndex
            {
                get => _bindingSwitch.PendingBindingIndex;
                set => _bindingSwitch.PendingBindingIndex = value;
            }
            public bool BindingReleasePending
            {
                get => _bindingSwitch.BindingReleasePending;
                set => _bindingSwitch.BindingReleasePending = value;
            }
            public RuntimeBridge[] Runtimes { get; }
            public CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator[] Evaluators { get; }
            public CharacterTestRuntimeAiLabLogicEventBuffer LocalEvents { get; } =
                new CharacterTestRuntimeAiLabLogicEventBuffer(16);

            public void SetFrame(
                CharacterTestCharacterEntity entity,
                CharacterTestRuntimeAiPlannerFactState facts,
                RuntimeFrame frame,
                CharacterTestCharacterActionRuntime actionRuntime,
                AbilityRuntimeModule abilityRuntime)
            {
                CurrentFrame = frame;
                CurrentFacts = facts;
                _decisionTrace.BeginFrame(
                    ClampFrame(frame.Value),
                    BuildCommittedState(ActiveBindingIndex, _bindings));
                CommandResultBindingIndex = -1;
                facts?.CaptureSelfHealth(entity, _traceFrame);
                UpdateFactHistory(facts, _traceFrame);
                for (int i = 0; i < Runtimes.Length; i++)
                    Runtimes[i].SetFrame(entity, facts, frame, actionRuntime, abilityRuntime);
            }

            public void RecordBindingEvaluation(int index, bool eligible)
            {
                if (index < 0 || index >= _bindingEvaluated.Length)
                    return;
                _bindingEvaluated[index] = true;
                _bindingEligible[index] = eligible;
            }

            public void RecordSelectedBinding(int index)
            {
                if (index < 0 || index >= _bindingEvaluated.Length)
                    return;
                _bindingEvaluated[index] = true;
                _bindingEligible[index] = true;
            }

            public void RecordCommandResult(RuntimeCommandValidationResult result)
            {
                _commandEnqueueKind = result.Success
                    ? CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued
                    : CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected;
                _commandReasonCode = result.Success
                    ? 0
                    : CharacterTestRuntimeAiLabDecisionTraceReasonCodes.CommandRejected;
            }

            public void RecordTerminalCommit(
                int bindingIndex,
                CharacterTestRuntimeAiLabLogicRuntimeState state)
            {
                if (bindingIndex < 0 || bindingIndex >= Runtimes.Length)
                    return;
                _terminalCommitted = BuildCommittedState(bindingIndex, _bindings);
                _terminalLogicKind = state == CharacterTestRuntimeAiLabLogicRuntimeState.Failed
                    ? CharacterTestRuntimeAiLabPlannerLogicCommitKind.Rejected
                    : CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed;
                RuntimeBridge runtime = Runtimes[bindingIndex];
                _terminalRuntimeEffectKind = runtime.RuntimeEffectKind;
                _terminalRuntimeEffectReasonCode = runtime.RuntimeEffectReasonCode;
                _terminalIntentKind = runtime.LastIntentKind;
                _hasTerminalCommit = true;
            }

            public void WriteDecisionTrace(
                CharacterTestRuntimeAiLabDecisionTraceWriter writer,
                IReadOnlyList<Binding> bindings,
                int frame)
            {
                if (_traceFrame != frame)
                    return;
                CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent = writer.BeginAgent(EntityId);
                if (!agent.IsValid)
                    return;
                bool hasPreviousCard = !_previousCommitted.CardIdentity.IsNone;
                bool hasPreviousStage = !_previousCommitted.StageIdentity.IsNone;
                CharacterTestRuntimeAiLabCommittedStateTrace previous = BuildCommittedState(
                    hasPreviousCard ? FindBinding(bindings, _previousCommitted.CardIdentity.SourceId) : -1,
                    bindings);
                if (hasPreviousCard || hasPreviousStage)
                    previous = _previousCommitted;
                agent.SetPrevious(previous);

                ulong cardArbitrationMask = 0UL;
                ulong logicConditionMask = 0UL;
                for (int i = 0; i < Runtimes.Length; i++)
                {
                    cardArbitrationMask |= Runtimes[i].CardArbitrationFactReadMask;
                    logicConditionMask |= Runtimes[i].LogicConditionFactReadMask;
                }
                for (int i = 0; i < _factHistory.Length; i++)
                {
                    DecisionTraceFactHistory fact = _factHistory[i];
                    CharacterTestRuntimeAiLabCardConditionFact kind = CharacterTestRuntimeAiLabFactCatalog.KindAt(i);
                    agent.AddFact(new CharacterTestRuntimeAiLabFactObservationTrace
                    {
                        Identity = CharacterTestAiEvidenceIds.LogicFact(kind),
                        SourceIdentity = CharacterTestAiEvidenceIds.Source(kind),
                        StableId = CharacterTestRuntimeAiLabFactCatalog.StableId(kind),
                        Domain = CharacterTestRuntimeAiLabDecisionTraceFactDomain.BehaviorCardLogic,
                        Fact = kind,
                        Value = fact.Value,
                        PreviousValue = fact.PreviousValue,
                        SourceStableId = CharacterTestRuntimeAiLabFactCatalog.SourceStableId(kind),
                        ObservedFrame = frame,
                        SourceFrame = kind == CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio
                            ? (CurrentFacts?.SelfHealthObservedFrame ?? 0) - 1 : -1,
                        SourceGeneration = 0L,
                        LastChangedFrame = fact.LastChangedFrame,
                        AuthorityKind = CharacterTestRuntimeAiLabFactCatalog.AuthorityKind(kind),
                        ParticipatedLayers =
                            ((cardArbitrationMask & CharacterTestRuntimeAiLabFactCatalog.Mask(kind)) != 0UL
                                ? CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration
                                : CharacterTestRuntimeAiLabDecisionTraceLayer.None)
                            | ((logicConditionMask & CharacterTestRuntimeAiLabFactCatalog.Mask(kind)) != 0UL
                                ? CharacterTestRuntimeAiLabDecisionTraceLayer.LogicCondition
                                : CharacterTestRuntimeAiLabDecisionTraceLayer.None),
                        BaselineUnknown = !fact.HasPrevious,
                        SourceUnknown = kind != CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio
                            || float.IsNaN(CurrentFacts?.SelfHealthRatio ?? float.NaN),
                        DerivationCode = CharacterTestRuntimeAiLabFactCatalog.DerivationCode(kind)
                    });
                }

                int selected = PendingBindingIndex >= 0 ? PendingBindingIndex : ActiveBindingIndex;
                for (int i = 0; i < bindings.Count; i++)
                {
                    Binding binding = bindings[i];
                    CharacterTestRuntimeAiLabDecisionTraceCandidateState state;
                    int reason;
                    if (!_bindingEvaluated[i])
                    {
                        state = CharacterTestRuntimeAiLabDecisionTraceCandidateState.NotEvaluated;
                        reason = CharacterTestRuntimeAiLabDecisionTraceReasonCodes.NotEvaluated;
                    }
                    else if (!_bindingEligible[i])
                    {
                        state = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Ineligible;
                        reason = binding.Enabled && binding.Program != null && !Runtimes[i].SystemActionUseResult.Available
                            ? 50000 + (int)Runtimes[i].SystemActionUseResult.Status
                            : CharacterTestRuntimeAiLabDecisionTraceReasonCodes.ConditionsNotMet;
                    }
                    else if (i == selected)
                    {
                        state = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected;
                        reason = CharacterTestRuntimeAiLabDecisionTraceReasonCodes.Selected;
                    }
                    else
                    {
                        state = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Evaluated;
                        reason = CharacterTestRuntimeAiLabDecisionTraceReasonCodes.LowerRank;
                    }
                    var candidate = new CharacterTestRuntimeAiLabCandidateEvaluationTrace
                    {
                        Identity = CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.CardKind, binding.NumericId),
                        StableId = binding.EntryId,
                        Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration,
                        State = state,
                        Score = _bindingEvaluated[i] ? binding.Score : 0f,
                        ScoreAvailable = _bindingEvaluated[i],
                        Priority = binding.Order,
                        PriorityAvailable = _bindingEvaluated[i],
                        Cost = 0,
                        CostAvailable = _bindingEvaluated[i],
                        StableRank = binding.Order,
                        ReasonCode = reason
                    };
                    if (!candidate.TryAddRelatedFact(FirstParticipatingFactStableId(i), FirstParticipatingFactIdentity(i)))
                        agent.RecordCandidateFactLinkOverflow();
                    agent.AddCandidate(candidate);
                }

                for (int runtimeIndex = 0; runtimeIndex < Runtimes.Length; runtimeIndex++)
                {
                    RuntimeBridge runtime = Runtimes[runtimeIndex];
                    for (int factIndex = 0; factIndex < runtime.ParameterFactSlotCount; factIndex++)
                    {
                        if (runtime.TryGetParameterFact(factIndex, frame, out var parameterFact))
                            agent.AddFact(parameterFact);
                    }
                    for (int candidateIndex = 0; candidateIndex < runtime.LogicCandidateCount; candidateIndex++)
                    {
                        CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate =
                            runtime.GetLogicCandidate(candidateIndex);
                        if (candidate.ReasonCode == CharacterTestRuntimeAiLabDecisionTraceReasonCodes.Overflow)
                        {
                            agent.RecordCandidateFactLinkOverflow();
                        }
                        agent.AddCandidate(candidate);
                    }
                    if (runtime.LogicCandidateOverflowed)
                    {
                        agent.RecordOverflow(
                            runtime.LogicCandidateOverflowRegion,
                            runtime.LogicCandidateOverflowActualCount,
                            runtime.LogicCandidateCapacity);
                    }
                }

                CharacterTestRuntimeAiLabCommittedStateTrace current = _hasTerminalCommit
                    ? _terminalCommitted
                    : BuildCommittedState(ActiveBindingIndex, bindings);
                CharacterTestRuntimeAiLabPlannerLogicCommitKind logicKind = _hasTerminalCommit
                    ? _terminalLogicKind
                    : ResolveLogicCommitKind(previous, current);
                RuntimeBridge activeRuntime = ActiveBindingIndex >= 0 && ActiveBindingIndex < Runtimes.Length
                    ? Runtimes[ActiveBindingIndex]
                    : null;
                CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind runtimeEffectKind = _hasTerminalCommit
                    ? _terminalRuntimeEffectKind
                    : activeRuntime?.RuntimeEffectKind
                        ?? CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved;
                int runtimeEffectReasonCode = _hasTerminalCommit
                    ? _terminalRuntimeEffectReasonCode
                    : activeRuntime?.RuntimeEffectReasonCode ?? 0;
                CharacterTestRuntimeAiLabRuntimeIntentKind intentKind = _hasTerminalCommit
                    ? _terminalIntentKind
                    : activeRuntime?.LastIntentKind ?? CharacterTestRuntimeAiLabRuntimeIntentKind.None;
                agent.SetCommit(new CharacterTestRuntimeAiLabCommitTrace
                {
                    CardIdentity = current.CardIdentity,
                    StageIdentity = current.StageIdentity,
                    MainActionIdentity = current.MainActionIdentity,
                    TransitionIdentity = current.PendingTransitionIdentity,
                    RuntimeEffectActionIdentity = current.MainActionIdentity,
                    PlannerLogicKind = logicKind,
                    CommandEnqueueKind = _commandEnqueueKind,
                    RuntimeEffectKind = runtimeEffectKind,
                    EvidenceFrame = frame,
                    PlannerEvidenceFrame = frame,
                    CommandEvidenceFrame = _commandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.NotRequested
                        ? -1
                        : frame,
                    RuntimeEffectEvidenceFrame = runtimeEffectKind !=
                        CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved
                            ? frame
                            : -1,
                    CardStableId = current.CardStableId,
                    StageStableId = current.StageStableId,
                    MainActionStableId = current.MainActionStableId,
                    TransitionStableId = current.PendingTransitionStableId,
                    CommandKind = intentKind != CharacterTestRuntimeAiLabRuntimeIntentKind.None
                            ? CharacterTestRuntimeAiLabDecisionTraceCommandKind.BehaviorCardIntent
                            : CharacterTestRuntimeAiLabDecisionTraceCommandKind.None,
                    CommandActionId = 0,
                    ReasonCode = ResolvePlannerReasonCode(logicKind),
                    PlannerReasonCode = ResolvePlannerReasonCode(logicKind),
                    CommandReasonCode = _commandReasonCode,
                    RuntimeEffectReasonCode = runtimeEffectReasonCode,
                    RuntimeEffectActionStableId = current.MainActionStableId
                });
            }

            private static int ResolvePlannerReasonCode(
                CharacterTestRuntimeAiLabPlannerLogicCommitKind kind)
            {
                switch (kind)
                {
                    case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed: return CharacterTestAiTraceReason.LOGIC_COMMITTED;
                    case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Held: return CharacterTestAiTraceReason.LOGIC_HELD;
                    case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Pending: return CharacterTestAiTraceReason.LOGIC_TRANSITION_PENDING;
                    case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Rejected: return CharacterTestAiTraceReason.LOGIC_REJECTED;
                    case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Fallback: return CharacterTestAiTraceReason.LOGIC_FALLBACK;
                    default: return CharacterTestAiTraceReason.LOGIC_NONE;
                }
            }

            private string FirstParticipatingFactStableId(int runtimeIndex)
            {
                if (runtimeIndex < 0 || runtimeIndex >= Runtimes.Length)
                    return string.Empty;
                ulong mask = Runtimes[runtimeIndex].FactReadMask;
                for (int i = 0; i < CharacterTestRuntimeAiLabFactCatalog.Count; i++)
                {
                    CharacterTestRuntimeAiLabCardConditionFact kind = CharacterTestRuntimeAiLabFactCatalog.KindAt(i);
                    if ((mask & CharacterTestRuntimeAiLabFactCatalog.Mask(kind)) != 0UL)
                        return CharacterTestRuntimeAiLabFactCatalog.StableId(kind);
                }
                return string.Empty;
            }

            private RuntimeTraceIdentity FirstParticipatingFactIdentity(int runtimeIndex)
            {
                if (runtimeIndex < 0 || runtimeIndex >= Runtimes.Length) return default;
                ulong mask = Runtimes[runtimeIndex].FactReadMask;
                for (int i = 0; i < CharacterTestRuntimeAiLabFactCatalog.Count; i++)
                    if ((mask & (1UL << i)) != 0) return CharacterTestAiEvidenceIds.LogicFact(CharacterTestRuntimeAiLabFactCatalog.KindAt(i));
                return default;
            }

            private CharacterTestRuntimeAiLabCommittedStateTrace BuildCommittedState(
                int bindingIndex,
                IReadOnlyList<Binding> bindings)
            {
                if (bindingIndex < 0 || bindingIndex >= Evaluators.Length || bindings == null)
                    return default;
                CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator evaluator = Evaluators[bindingIndex];
                Binding binding = bindings[bindingIndex];
                int stageIndex = evaluator.CurrentStageIndex;
                CharacterTestRuntimeAiLabCompiledLogicStage stage = stageIndex >= 0
                    && stageIndex < binding.Program.Stages.Count
                        ? binding.Program.Stages[stageIndex]
                        : null;
                int transitionIndex = evaluator.PendingTransitionIndex >= 0
                    ? evaluator.PendingTransitionIndex
                    : evaluator.CommittedTransitionIndexThisFrame;
                string transition = transitionIndex >= 0 && transitionIndex < binding.Program.Transitions.Count
                    ? binding.Program.Transitions[transitionIndex].TransitionId
                    : string.Empty;
                return new CharacterTestRuntimeAiLabCommittedStateTrace
                {
                    CardIdentity = CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.CardKind, binding.NumericId),
                    StageIdentity = CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.StageKind, stageIndex, binding.NumericId),
                    MainActionIdentity = Runtimes[bindingIndex].MainActionIdentity(stageIndex),
                    PendingTransitionIdentity = CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.TransitionKind, transitionIndex, binding.NumericId),
                    CardStableId = binding.EntryId,
                    StageStableId = binding.Program.Execution != null ? binding.Program.ExecutionDebugMap?.Timeline(evaluator.CurrentTimelineIndex) ?? string.Empty : stage?.StageId ?? string.Empty,
                    MainActionStableId = Runtimes[bindingIndex].MainActionStableId(stageIndex),
                    PendingTransitionStableId = transition
                };
            }

            private static int FindBinding(IReadOnlyList<Binding> bindings, int numericId)
            {
                for (int i = 0; bindings != null && i < bindings.Count; i++)
                {
                    if (bindings[i].NumericId == numericId)
                        return i;
                }
                return -1;
            }

            private CharacterTestRuntimeAiLabPlannerLogicCommitKind ResolveLogicCommitKind(
                in CharacterTestRuntimeAiLabCommittedStateTrace previous,
                in CharacterTestRuntimeAiLabCommittedStateTrace current)
            {
                if (BindingReleasePending || PendingBindingIndex >= 0
                    || (ActiveBindingIndex >= 0 && Evaluators[ActiveBindingIndex].PendingTransitionIndex >= 0))
                {
                    return CharacterTestRuntimeAiLabPlannerLogicCommitKind.Pending;
                }
                if (ActiveBindingIndex >= 0
                    && Evaluators[ActiveBindingIndex].State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed)
                {
                    return CharacterTestRuntimeAiLabPlannerLogicCommitKind.Rejected;
                }
                if (current.CardIdentity.IsNone)
                    return CharacterTestRuntimeAiLabPlannerLogicCommitKind.None;
                return previous.CardIdentity != current.CardIdentity
                    || previous.StageIdentity != current.StageIdentity
                        ? CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed
                        : CharacterTestRuntimeAiLabPlannerLogicCommitKind.Held;
            }

            private void UpdateFactHistory(CharacterTestRuntimeAiPlannerFactState facts, int frame)
            {
                for (int i = 0; i < _factHistory.Length; i++)
                {
                    CharacterTestRuntimeAiLabCardConditionFact kind = CharacterTestRuntimeAiLabFactCatalog.KindAt(i);
                    CharacterTestRuntimeAiLabDecisionTraceValue value =
                        CharacterTestRuntimeAiLabFactCatalog.ReadValue(kind, facts);
                    DecisionTraceFactHistory previous = _factHistory[i];
                    _factHistory[i].PreviousValue = previous.Value;
                    _factHistory[i].HasPrevious = previous.Initialized;
                    _factHistory[i].Value = value;
                    _factHistory[i].Initialized = true;
                    _factHistory[i].LastChangedFrame = !previous.Initialized || !previous.Value.Equals(value)
                        ? frame
                        : previous.LastChangedFrame;
                }
            }

            public void RestoreDecisionTraceHistory(
                IReadOnlyList<CharacterTestRuntimeAiLabFactObservationTrace> source)
            {
                Array.Clear(_factHistory, 0, _factHistory.Length);
                for (int i = 0; source != null && i < source.Count; i++)
                {
                    CharacterTestRuntimeAiLabFactObservationTrace fact = source[i];
                    if (fact.Domain != CharacterTestRuntimeAiLabDecisionTraceFactDomain.BehaviorCardLogic)
                        continue;
                    int index = (int)fact.Fact;
                    if (index < 0 || index >= _factHistory.Length)
                        continue;
                    _factHistory[index] = new DecisionTraceFactHistory
                    {
                        Initialized = true,
                        HasPrevious = !fact.BaselineUnknown,
                        Value = fact.Value,
                        PreviousValue = fact.PreviousValue,
                        LastChangedFrame = fact.LastChangedFrame
                    };
                }
                for (int i = 0; i < Runtimes.Length; i++)
                    Runtimes[i].RestoreParameterizedFactHistory(source);
            }

            internal sealed class StateCaptureStorage
            {
                internal readonly CharacterTestRuntimeAiLabLogicEvaluatorSnapshot[] Evaluators;
                internal readonly CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] Snapshots;
                internal readonly CharacterTestRuntimeAiLabLogicAgentStateSnapshot Idle = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
                {
                    DetectorArmed = Array.Empty<bool>()
                };
                internal StateCaptureStorage(AgentInstance source, IReadOnlyList<Binding> bindings)
                {
                    Evaluators = new CharacterTestRuntimeAiLabLogicEvaluatorSnapshot[bindings.Count];
                    Snapshots = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot[bindings.Count];
                    for (int i = 0; i < bindings.Count; i++)
                    {
                        Evaluators[i] = source.Evaluators[i].CreateSnapshot();
                        Snapshots[i] = source.Runtimes[i].CreateStateSnapshot(source.EntityId, i,
                            bindings[i].Program.SemanticHash, Evaluators[i], bindings[i].Program);
                    }
                }
            }

            internal CharacterTestRuntimeAiLabLogicAgentStateSnapshot CaptureState(
                IReadOnlyList<Binding> bindings, StateCaptureStorage storage)
            {
                if (AutomaticExecutionId != 0) throw new InvalidOperationException("execution:AutomaticOwnerSnapshotUnsupported");
                int index = ActiveBindingIndex >= 0 ? ActiveBindingIndex : PendingBindingIndex;
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot result;
                if (index >= 0 && index < bindings.Count)
                {
                    var evaluator = Evaluators[index].CreateSnapshot(storage.Evaluators[index]);
                    result = Runtimes[index].CreateStateSnapshot(EntityId, ActiveBindingIndex,
                        bindings[index].Program.SemanticHash, evaluator, bindings[index].Program, storage.Snapshots[index]);
                    result.BehaviorCardId = bindings[index].NumericId;
                }
                else result = storage.Idle;
                result.EntityId = EntityId;
                result.ActiveBindingIndex = ActiveBindingIndex;
                result.PendingBindingIndex = PendingBindingIndex;
                result.BindingReleasePending = BindingReleasePending;
                return result;
            }

            public CharacterTestRuntimeAiLabLogicAgentStateSnapshot CreateStateSnapshot(
                IReadOnlyList<Binding> bindings)
            {
                if (AutomaticExecutionId != 0) throw new InvalidOperationException("execution:AutomaticOwnerSnapshotUnsupported");
                if (ActiveBindingIndex < 0)
                {
                    if (PendingBindingIndex >= 0 && PendingBindingIndex < bindings.Count)
                    {
                        CharacterTestRuntimeAiLabLogicEvaluatorSnapshot pendingEvaluator =
                            Evaluators[PendingBindingIndex].CreateSnapshot();
                        CharacterTestRuntimeAiLabLogicAgentStateSnapshot pendingSnapshot =
                            Runtimes[PendingBindingIndex].CreateStateSnapshot(
                                EntityId,
                                -1,
                                bindings[PendingBindingIndex].Program.SemanticHash,
                                pendingEvaluator,
                                bindings[PendingBindingIndex].Program);
                        pendingSnapshot.PendingBindingIndex = PendingBindingIndex;
                        pendingSnapshot.BindingReleasePending = BindingReleasePending;
                        pendingSnapshot.BehaviorCardId =
                            bindings[PendingBindingIndex].NumericId;
                        return pendingSnapshot;
                    }
                    return new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
                    {
                        EntityId = EntityId,
                        ActiveBindingIndex = -1,
                        PendingBindingIndex = PendingBindingIndex,
                        BindingReleasePending = BindingReleasePending,
                        PrimaryActionStageIndex = -1,
                        DetectorArmed = Array.Empty<bool>()
                    };
                }
                CharacterTestRuntimeAiLabLogicEvaluatorSnapshot evaluator =
                    Evaluators[ActiveBindingIndex].CreateSnapshot();
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot snapshot =
                    Runtimes[ActiveBindingIndex].CreateStateSnapshot(
                    EntityId,
                    ActiveBindingIndex,
                    bindings[ActiveBindingIndex].Program.SemanticHash,
                    evaluator,
                    bindings[ActiveBindingIndex].Program);
                snapshot.PendingBindingIndex = PendingBindingIndex;
                snapshot.BindingReleasePending = BindingReleasePending;
                snapshot.BehaviorCardId = bindings[ActiveBindingIndex].NumericId;
                return snapshot;
            }

            public bool TryApplyStateSnapshot(
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot snapshot,
                IReadOnlyList<Binding> bindings,
                out string failure)
            {
                failure = string.Empty;
                if ((snapshot.ActiveBindingIndex >= 0 && snapshot.ActiveBindingIndex < bindings.Count && bindings[snapshot.ActiveBindingIndex].Automatic != null)
                    || (snapshot.PendingBindingIndex >= 0 && snapshot.PendingBindingIndex < bindings.Count && bindings[snapshot.PendingBindingIndex].Automatic != null))
                { failure = "execution:AutomaticOwnerSnapshotUnsupported"; return false; }
                ActiveBindingIndex = snapshot.ActiveBindingIndex;
                PendingBindingIndex = snapshot.PendingBindingIndex;
                BindingReleasePending = snapshot.BindingReleasePending;
                if (ActiveBindingIndex < -1)
                {
                    failure = "Behavior-card snapshot references an invalid active binding.";
                    return false;
                }
                if (PendingBindingIndex < -1 || PendingBindingIndex >= bindings.Count)
                {
                    failure = "Behavior-card snapshot references an invalid pending binding.";
                    return false;
                }
                int identityBindingIndex = ActiveBindingIndex >= 0
                    ? ActiveBindingIndex
                    : PendingBindingIndex;
                if (identityBindingIndex >= 0
                    && (snapshot.BehaviorCardId <= 0 || snapshot.BehaviorCardId != bindings[identityBindingIndex].NumericId))
                {
                    failure = "Behavior-card snapshot identity mismatch. snapshot="
                        + snapshot.BehaviorCardId
                        + ", runtime="
                        + bindings[identityBindingIndex].EntryId
                        + ", stateType=BehaviorRecipeTimelineEvent.";
                    return false;
                }
                if (BindingReleasePending && ActiveBindingIndex < 0)
                {
                    failure = "Behavior-card snapshot cannot release without an active binding.";
                    return false;
                }
                if (!BindingReleasePending && ActiveBindingIndex >= 0 && PendingBindingIndex >= 0)
                {
                    failure = "Behavior-card snapshot cannot own active and pending bindings together.";
                    return false;
                }
                if (ActiveBindingIndex < 0)
                {
                    if (PendingBindingIndex >= 0)
                    {
                        if (bindings[PendingBindingIndex].Program.SemanticHash != snapshot.ProgramSemanticHash
                            || !Runtimes[PendingBindingIndex].TryRestoreStateSnapshot(
                                snapshot,
                                bindings[PendingBindingIndex].Program))
                        {
                            failure = "Behavior-card pending entry handshake could not be restored.";
                            return false;
                        }
                    }
                    return true;
                }
                if (ActiveBindingIndex >= bindings.Count
                    || bindings[ActiveBindingIndex].Program.SemanticHash != snapshot.ProgramSemanticHash)
                {
                    failure = "Behavior-card snapshot references a missing or changed program.";
                    return false;
                }
                var evaluator = new CharacterTestRuntimeAiLabLogicEvaluatorSnapshot(
                    (CharacterTestRuntimeAiLabLogicRuntimeState)snapshot.RuntimeState,
                    snapshot.CurrentTimelineIndex >= 0
                        ? snapshot.CurrentTimelineIndex
                        : snapshot.CurrentStageIndex,
                    snapshot.CardStartedFrame,
                    snapshot.TimelineEnteredFrame >= 0
                        ? snapshot.TimelineEnteredFrame
                        : snapshot.StageEnteredFrame,
                    snapshot.PendingTransitionIndex,
                    snapshot.TriggerArmed != null && snapshot.TriggerArmed.Length > 0
                        ? snapshot.TriggerArmed
                        : snapshot.DetectorArmed ?? Array.Empty<bool>(),
                    snapshot.LastEvaluatedRuntimeFrame >= 0
                        ? snapshot.LastEvaluatedRuntimeFrame
                        : (snapshot.TimelineEnteredFrame >= 0
                            ? snapshot.TimelineEnteredFrame
                            : snapshot.StageEnteredFrame) + Math.Max(0, snapshot.LocalFrame),
                    snapshot.PrimaryActionInstanceId,
                    snapshot.TriggerFired ?? Array.Empty<bool>(),
                    snapshot.LastTriggeredEventIndex,
                    snapshot.EventExecutionCounts ?? Array.Empty<int>(),
                    snapshot.EventLastExecutionFrames ?? Array.Empty<int>(),
                    snapshot.EventResults ?? Array.Empty<int>(),
                    snapshot.PreviousEventResults ?? Array.Empty<int>(),
                    snapshot.ActorOwnerEventIndex,
                    snapshot.PendingEventIndex,
                    snapshot.PendingTimelineIndex,
                    snapshot.PendingV6Operation,
                    snapshot.LocalPlayheadFrame,
                    snapshot.TimelinePassGeneration,
                    snapshot.LoopRepeatCounts ?? Array.Empty<int>(),
                    snapshot.ActiveWaitEventIndex,
                    snapshot.PendingPlayheadTargetFrame,
                    snapshot.PendingTimeControlEventIndex,
                    snapshot.WaitingForInstantOwnerAtTimelineEnd,
                    snapshot.LastSampledPlayheadFrame, snapshot.ExecutionState);
                if (!Evaluators[ActiveBindingIndex].TryRestoreSnapshot(evaluator)
                    || !Runtimes[ActiveBindingIndex].TryRestoreStateSnapshot(
                        snapshot,
                        bindings[ActiveBindingIndex].Program))
                {
                    failure = "Behavior-card agent state could not be restored.";
                    return false;
                }
                if (BindingReleasePending && PendingBindingIndex >= 0)
                {
                    CharacterTestRuntimeAiLabCompiledLogicProgram pendingProgram =
                        bindings[PendingBindingIndex].Program;
                    CharacterTestRuntimeAiLabCompiledLogicStage pendingEntry = pendingProgram != null
                        && pendingProgram.EntryStageIndex >= 0
                        && pendingProgram.EntryStageIndex < pendingProgram.Stages.Count
                            ? pendingProgram.Stages[pendingProgram.EntryStageIndex]
                            : null;
                    if (!Runtimes[PendingBindingIndex].IsCharacterActionEntry(pendingEntry))
                        Runtimes[ActiveBindingIndex].TryInterruptPrimaryAction();
                }
                return true;
            }


        }
    }
}
