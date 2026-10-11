using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay
    {
        private sealed class BindingSwitchCoordinator
        {
            public int ActiveBindingIndex { get; set; } = -1;
            public int PendingBindingIndex { get; set; } = -1;
            public int CommandResultBindingIndex { get; set; } = -1;
            public bool BindingReleasePending { get; set; }

            public void ClearPending()
            {
                PendingBindingIndex = -1;
                BindingReleasePending = false;
            }
        }

        private sealed class EntryActionHandshake
        {
            public bool CommandPending;
            public bool CommandRejected;
            public bool ActionObserved;
            public bool ActionStartedThisFrame;
            public bool AdoptEntryStagePending;
            public long PreviousInstanceId;
            public long ObservedInstanceId;

            public void BeginFrame()
            {
                ActionStartedThisFrame = false;
            }

            public void Reset()
            {
                CommandPending = false;
                CommandRejected = false;
                ActionObserved = false;
                ActionStartedThisFrame = false;
                AdoptEntryStagePending = false;
                PreviousInstanceId = 0L;
                ObservedInstanceId = 0L;
            }
        }

        private struct DecisionTraceFactHistory
        {
            public bool Initialized;
            public bool HasPrevious;
            public CharacterTestRuntimeAiLabDecisionTraceValue Value;
            public CharacterTestRuntimeAiLabDecisionTraceValue PreviousValue;
            public int LastChangedFrame;
        }

        private sealed class DecisionTraceState
        {
            public DecisionTraceState(int bindingCount)
            {
                BindingEvaluated = new bool[bindingCount];
                BindingEligible = new bool[bindingCount];
                FactHistory = new DecisionTraceFactHistory[CharacterTestRuntimeAiLabFactCatalog.Count];
            }

            public bool[] BindingEvaluated { get; }
            public bool[] BindingEligible { get; }
            public DecisionTraceFactHistory[] FactHistory { get; }
            public CharacterTestRuntimeAiLabCommittedStateTrace PreviousCommitted;
            public CharacterTestRuntimeAiLabCommandEnqueueKind CommandEnqueueKind;
            public int CommandReasonCode = 0;
            public int TraceFrame = -1;
            public bool HasTerminalCommit;
            public CharacterTestRuntimeAiLabCommittedStateTrace TerminalCommitted;
            public CharacterTestRuntimeAiLabPlannerLogicCommitKind TerminalLogicKind;
            public CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind TerminalRuntimeEffectKind;
            public int TerminalRuntimeEffectReasonCode = 0;
            public CharacterTestRuntimeAiLabRuntimeIntentKind TerminalIntentKind;

            public void BeginFrame(
                int frame,
                in CharacterTestRuntimeAiLabCommittedStateTrace previousCommitted)
            {
                TraceFrame = frame;
                PreviousCommitted = previousCommitted;
                CommandEnqueueKind = CharacterTestRuntimeAiLabCommandEnqueueKind.NotRequested;
                CommandReasonCode = 0;
                HasTerminalCommit = false;
                TerminalCommitted = default;
                TerminalLogicKind = CharacterTestRuntimeAiLabPlannerLogicCommitKind.None;
                TerminalRuntimeEffectKind = CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved;
                TerminalRuntimeEffectReasonCode = 0;
                TerminalIntentKind = CharacterTestRuntimeAiLabRuntimeIntentKind.None;
                Array.Clear(BindingEvaluated, 0, BindingEvaluated.Length);
                Array.Clear(BindingEligible, 0, BindingEligible.Length);
            }
        }

        private sealed class RuntimeDecisionTraceState
        {
            public RuntimeDecisionTraceState(
                string bindingStableId,
                CharacterTestRuntimeAiLabCompiledLogicProgram program)
            {
                int detectorCount = program?.Detectors.Count ?? 0;
                int stageCount = program?.Stages.Count ?? 0;
                int transitionCount = program?.Transitions.Count ?? 0;
                LogicCandidates = new CharacterTestRuntimeAiLabCandidateEvaluationTrace[
                    Math.Max(1, detectorCount + stageCount + transitionCount)];
                DetectorCandidateStableIds = new string[detectorCount];
                StageCandidateStableIds = new string[stageCount];
                StageMainActionStableIds = new string[stageCount];
                StageHasMainAction = new bool[stageCount];
                TransitionCandidateStableIds = new string[transitionCount];
                string prefix = string.IsNullOrEmpty(bindingStableId) ? "card" : bindingStableId;
                for (int i = 0; i < detectorCount; i++)
                    DetectorCandidateStableIds[i] = prefix + "/detector/" + program.Detectors[i].DetectorId;
                for (int i = 0; i < stageCount; i++)
                {
                    StageCandidateStableIds[i] = prefix + "/stage/" + program.Stages[i].StageId;
                    string mainActionId = RuntimeBridge.BuildMainActionStableId(program.Stages[i]);
                    StageHasMainAction[i] = !string.IsNullOrEmpty(mainActionId);
                    StageMainActionStableIds[i] = string.IsNullOrEmpty(mainActionId)
                        ? string.Empty
                        : prefix + "/action/" + mainActionId;
                }
                for (int i = 0; i < transitionCount; i++)
                    TransitionCandidateStableIds[i] = prefix + "/transition/" + program.Transitions[i].TransitionId;
            }

            public CharacterTestRuntimeAiLabCandidateEvaluationTrace[] LogicCandidates { get; }
            public string[] DetectorCandidateStableIds { get; }
            public string[] StageCandidateStableIds { get; }
            public string[] StageMainActionStableIds { get; }
            public bool[] StageHasMainAction { get; }
            public string[] TransitionCandidateStableIds { get; }
            public int LogicCandidateCount;
            public int ActiveLogicCandidateIndex = -1;
            public bool LogicCandidateOverflowed;
            public CharacterTestRuntimeAiLabDecisionTraceOverflowRegion LogicCandidateOverflowRegion;
            public int LogicCandidateOverflowActualCount;

            public void BeginFrame()
            {
                LogicCandidateCount = 0;
                ActiveLogicCandidateIndex = -1;
                LogicCandidateOverflowed = false;
                LogicCandidateOverflowRegion = CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.None;
                LogicCandidateOverflowActualCount = 0;
            }

            public void LinkActiveCandidate(string stableFactId, RuntimeTraceIdentity identity)
            {
                int index = ActiveLogicCandidateIndex;
                if (index < 0 || index >= LogicCandidateCount)
                    return;
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = LogicCandidates[index];
                if (!candidate.TryAddRelatedFact(stableFactId, identity))
                    candidate.ReasonCode = CharacterTestRuntimeAiLabDecisionTraceReasonCodes.Overflow;
                LogicCandidates[index] = candidate;
            }
        }

        private sealed class LogicFactRuntime
        {
            private readonly int _entityId;
            private readonly RuntimeDecisionTraceState _decisionTrace;
            private readonly string[] _conditionIds;
            private readonly string[] _stableIds;
            private readonly CharacterTestRuntimeAiLabCardConditionFact[] _kinds;
            private readonly ParameterFactHistory[] _history;
            private readonly CharacterTestRuntimeAiLabFactObservationTrace[] _observations;
            private readonly int[] _observedFrames;
            private int _slotCount;
            private CharacterTestRuntimeAiPlannerFactState _facts;
            private float _selfHealthRatio = float.NaN;
            private CharacterTestCharacterActionRuntime _actionRuntime;
            private AbilityRuntimeModule _abilityRuntime;
            private RuntimeFrame _frame;
            private CharacterTestRuntimeAiLabDecisionTraceLayer _traceLayer;

            public LogicFactRuntime(
                int entityId,
                string bindingStableId,
                CharacterTestRuntimeAiLabCompiledLogicProgram program,
                RuntimeDecisionTraceState decisionTrace, int bindingNumericId)
            {
                _entityId = entityId;
                _bindingNumericId = bindingNumericId;
                _decisionTrace = decisionTrace ?? throw new ArgumentNullException(nameof(decisionTrace));
                int capacity = CountConditions(program);
                _conditionIds = new string[capacity];
                _stableIds = new string[capacity];
                _kinds = new CharacterTestRuntimeAiLabCardConditionFact[capacity];
                _history = new ParameterFactHistory[capacity];
                _observations = new CharacterTestRuntimeAiLabFactObservationTrace[capacity];
                _observedFrames = new int[capacity];
                for (int i = 0; i < _observedFrames.Length; i++)
                    _observedFrames[i] = -1;
                CollectParameterizedConditions(
                    program,
                    string.IsNullOrEmpty(bindingStableId) ? "card" : bindingStableId);
            }

            public ulong FactReadMask { get; private set; }
            public ulong CardArbitrationFactReadMask { get; private set; }
            public ulong LogicConditionFactReadMask { get; private set; }
            public int ParameterFactSlotCount => _slotCount;
            public Vector2 MoveDirection => _facts?.MoveDirection ?? Vector2.zero;

            public void SetFrame(
                CharacterTestRuntimeAiPlannerFactState facts,
                RuntimeFrame frame,
                CharacterTestCharacterActionRuntime actionRuntime,
                AbilityRuntimeModule abilityRuntime, CharacterTestCharacterEntity self = null)
            {
                _facts = facts;
                _facts?.CaptureSelfHealth(self, ClampFrame(frame.Value));
                _selfHealthRatio = _facts?.SelfHealthRatio ?? float.NaN;
                _frame = frame;
                _actionRuntime = actionRuntime;
                _abilityRuntime = abilityRuntime;
                FactReadMask = 0UL;
                CardArbitrationFactReadMask = 0UL;
                LogicConditionFactReadMask = 0UL;
                _traceLayer = CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration;
                InvalidateObservations();
            }

            public void Reset()
            {
                FactReadMask = 0UL;
                CardArbitrationFactReadMask = 0UL;
                LogicConditionFactReadMask = 0UL;
                InvalidateObservations();
            }

            public void SetTraceLayer(CharacterTestRuntimeAiLabDecisionTraceLayer layer)
            {
                _traceLayer = layer;
            }

            public bool CanActivate(CharacterTestRuntimeAiLabCompiledLogicProgram program)
            {
                for (int i = 0; i < program.ActivationConditions.Count; i++)
                {
                    if (!Evaluate(program.ActivationConditions[i]))
                        return false;
                }
                return true;
            }

            public bool CanRemainActive(CharacterTestRuntimeAiLabCompiledLogicProgram program)
            {
                for (int i = 0; i < program.ActivationConditions.Count; i++)
                {
                    CharacterTestRuntimeAiLabCompiledCondition condition = program.ActivationConditions[i];
                    if (IsEntryOnlyCondition(condition.Fact))
                        continue;
                    if (!Evaluate(condition))
                        return false;
                }
                return true;
            }

            public bool Evaluate(in CharacterTestRuntimeAiLabCompiledCondition condition)
            {
                ulong mask = CharacterTestRuntimeAiLabFactCatalog.Mask(condition.Fact);
                FactReadMask |= mask;
                if (_traceLayer == CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration)
                    CardArbitrationFactReadMask |= mask;
                else
                    LogicConditionFactReadMask |= mask;
                ResolveTraceReference(condition, out string factLabel, out RuntimeTraceIdentity factIdentity);
                _decisionTrace.LinkActiveCandidate(factLabel, factIdentity);

                switch (condition.Fact)
                {
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetState:
                        if (_facts == null)
                            return false;
                        return CharacterTestRuntimeAiPlannerTargetStatePolicy.Matches(
                            _facts.TargetStateMask,
                            condition.AllowedTargetPhases,
                            condition.RequiredTargetFlags,
                            condition.ForbiddenTargetFlags);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                        return CompareBool(_facts != null && _facts.TargetExists, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                        return CompareBool(_facts != null && _facts.TargetVisible, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                        return CompareBool(_facts != null && _facts.TargetDead, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetDistance:
                        return _facts != null
                            && _facts.TargetDistanceAvailable
                            && CompareFloat(_facts.TargetDistance, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetDistanceAvailable:
                        return CompareBool(
                            _facts != null && _facts.TargetDistanceAvailable,
                            condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance:
                        return CompareFloat(_facts?.TargetRootDistance ?? 0f, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                        return CompareBool(_facts != null && _facts.SelfActionRunning, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                        return CompareBool(
                            _facts != null && _facts.SelfActionInterruptible,
                            condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio:
                        return !float.IsNaN(_selfHealthRatio) && CompareFloat(_selfHealthRatio, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                        bool canTransition = condition.CharacterActionId.IsValid
                            ? _actionRuntime != null
                                && _actionRuntime.CanTransitionToAction(
                                    new CharacterTestCharacterEntityId(_entityId),
                                    condition.CharacterActionId)
                            : _facts == null || !_facts.SelfActionRunning;
                        RecordParameterizedFact(
                            condition,
                            CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(canTransition));
                        return CompareBool(canTransition, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                        bool abilityAvailable = IsAbilityAvailable(condition.AbilityId);
                        RecordParameterizedFact(
                            condition,
                            CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(abilityAvailable));
                        return CompareBool(abilityAvailable, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                        return _facts != null && _facts.NavigationObserved && CompareBool(_facts.MovePathAvailable, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                        return CompareBool(_facts != null && _facts.TargetAttackWillHitSelf, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames:
                        return CompareFloat(_facts?.TargetAttackImpactFrames ?? -1, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                        return CompareBool(_facts != null && _facts.TargetAttackInstanceResponded, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                        return CompareBool(_facts != null && _facts.SelfDodgeCooldownReady, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                        return _facts != null && _facts.PrimaryAttackFactObserved && CompareBool(_facts.TargetInAttackRange, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                        return CompareBool(_facts != null && _facts.TargetLineOfSightBlocked, condition);
                    case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                        return _facts != null && _facts.EnvironmentObserved && CompareBool(_facts != null && _facts.EnvironmentNearHazard, condition);
                    default:
                        return false;
                }
            }

            public bool TryGetParameterFact(
                int slot,
                int frame,
                out CharacterTestRuntimeAiLabFactObservationTrace observation)
            {
                if (slot >= 0 && slot < _slotCount && _observedFrames[slot] == frame)
                {
                    observation = _observations[slot];
                    return true;
                }
                observation = default;
                return false;
            }

            public void RestoreParameterizedFactHistory(
                IReadOnlyList<CharacterTestRuntimeAiLabFactObservationTrace> source)
            {
                Array.Clear(_history, 0, _history.Length);
                InvalidateObservations();
                for (int slot = 0; slot < _slotCount; slot++)
                {
                    for (int i = 0; source != null && i < source.Count; i++)
                    {
                        CharacterTestRuntimeAiLabFactObservationTrace fact = source[i];
                        if (!string.Equals(fact.StableId, _stableIds[slot], StringComparison.Ordinal))
                            continue;
                        _history[slot] = new ParameterFactHistory
                        {
                            Initialized = true,
                            Value = fact.Value,
                            PreviousValue = fact.PreviousValue,
                            LastChangedFrame = fact.LastChangedFrame
                        };
                        break;
                    }
                }
            }

            private bool IsAbilityAvailable(int abilityId)
            {
                if (abilityId <= 0 || _abilityRuntime == null
                    || !_abilityRuntime.Definitions.TryGet(abilityId, out AbilityDefinitionConfig ability))
                {
                    return false;
                }
                return _abilityRuntime.GetRemainingCooldownFrames(_entityId, ability, _frame) <= 0L;
            }

            private void InvalidateObservations()
            {
                Array.Clear(_observations, 0, _observations.Length);
                for (int i = 0; i < _observedFrames.Length; i++)
                    _observedFrames[i] = -1;
            }

            private static int CountConditions(CharacterTestRuntimeAiLabCompiledLogicProgram program)
            {
                if (program == null)
                    return 0;
                int count = program.ActivationConditions.Count;
                for (int i = 0; i < program.Stages.Count; i++)
                    count += program.Stages[i].EntryConditions.Count;
                for (int i = 0; i < program.Detectors.Count; i++)
                    count += program.Detectors[i].Conditions.Count;
                return count;
            }

            private void CollectParameterizedConditions(
                CharacterTestRuntimeAiLabCompiledLogicProgram program,
                string prefix)
            {
                if (program == null)
                    return;
                for (int i = 0; i < program.ActivationConditions.Count; i++)
                    Register(program.ActivationConditions[i], prefix);
                for (int stage = 0; stage < program.Stages.Count; stage++)
                {
                    IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> conditions =
                        program.Stages[stage].EntryConditions;
                    for (int i = 0; i < conditions.Count; i++)
                        Register(conditions[i], prefix);
                }
                for (int detector = 0; detector < program.Detectors.Count; detector++)
                {
                    IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> conditions =
                        program.Detectors[detector].Conditions;
                    for (int i = 0; i < conditions.Count; i++)
                        Register(conditions[i], prefix);
                }
            }

            private void Register(in CharacterTestRuntimeAiLabCompiledCondition condition, string prefix)
            {
                if (condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                    && condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable)
                {
                    return;
                }
                for (int i = 0; i < _slotCount; i++)
                {
                    if (string.Equals(_conditionIds[i], condition.ConditionId, StringComparison.Ordinal))
                        return;
                }
                int slot = _slotCount++;
                _conditionIds[slot] = condition.ConditionId;
                _kinds[slot] = condition.Fact;
                _stableIds[slot] = prefix + "/fact/" + (condition.Fact ==
                    CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                        ? condition.ConditionId + "/action/" + condition.CharacterActionId
                        : condition.ConditionId + "/ability/" + condition.AbilityId.ToString(
                            System.Globalization.CultureInfo.InvariantCulture));
            }

            private string ResolveStableId(in CharacterTestRuntimeAiLabCompiledCondition condition)
            {
                if (condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                    && condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable)
                {
                    return CharacterTestRuntimeAiLabFactCatalog.StableId(condition.Fact);
                }
                for (int i = 0; i < _slotCount; i++)
                {
                    if (string.Equals(_conditionIds[i], condition.ConditionId, StringComparison.Ordinal))
                        return _stableIds[i];
                }
                return CharacterTestRuntimeAiLabFactCatalog.StableId(condition.Fact);
            }

            private readonly int _bindingNumericId;
            private void ResolveTraceReference(in CharacterTestRuntimeAiLabCompiledCondition condition, out string label, out RuntimeTraceIdentity identity)
            {
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                    || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable)
                    for (int i = 0; i < _slotCount; i++)
                        if (_conditionIds[i] == condition.ConditionId)
                        {
                            label = _stableIds[i];
                            identity = CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.ParameterFactKind, i, _bindingNumericId);
                            return;
                        }
                label = CharacterTestRuntimeAiLabFactCatalog.StableId(condition.Fact);
                identity = CharacterTestAiEvidenceIds.LogicFact(condition.Fact);
            }

            private void RecordParameterizedFact(
                in CharacterTestRuntimeAiLabCompiledCondition condition,
                in CharacterTestRuntimeAiLabDecisionTraceValue value)
            {
                int slot = -1;
                for (int i = 0; i < _slotCount; i++)
                {
                    if (string.Equals(_conditionIds[i], condition.ConditionId, StringComparison.Ordinal))
                    {
                        slot = i;
                        break;
                    }
                }
                if (slot < 0)
                    return;

                int frame = ClampFrame(_frame.Value);
                ParameterFactHistory history = _history[slot];
                if (!history.Initialized || !history.Value.Equals(value))
                    history.LastChangedFrame = frame;
                var observation = new CharacterTestRuntimeAiLabFactObservationTrace
                {
                    Identity = CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.ParameterFactKind, slot, _bindingNumericId),
                    StableId = _stableIds[slot],
                    Domain = CharacterTestRuntimeAiLabDecisionTraceFactDomain.BehaviorCardLogic,
                    Fact = _kinds[slot],
                    Value = value,
                    PreviousValue = history.Value,
                    SourceStableId = string.Empty,
                    ObservedFrame = frame,
                    SourceFrame = -1,
                    SourceGeneration = 0L,
                    LastChangedFrame = history.LastChangedFrame,
                    AuthorityKind = CharacterTestRuntimeAiLabDecisionTraceAuthorityKind.Derived,
                    ParticipatedLayers = _traceLayer,
                    BaselineUnknown = !history.Initialized,
                    SourceUnknown = true,
                    DerivationCode = CharacterTestRuntimeAiLabFactCatalog.DerivationCode(condition.Fact)
                };
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction)
                    observation.TryAddDependency("character-action.runtime.transition", new RuntimeTraceIdentity(CharacterTestAiEvidenceIds.ActionTransitionKind));
                else
                {
                    observation.TryAddDependency("ability.runtime.definition", new RuntimeTraceIdentity(CharacterTestAiEvidenceIds.AbilityDefinitionKind));
                    observation.TryAddDependency("ability.runtime.cooldown", new RuntimeTraceIdentity(CharacterTestAiEvidenceIds.AbilityCooldownKind));
                }
                history.PreviousValue = history.Value;
                history.Value = value;
                history.Initialized = true;
                _history[slot] = history;
                _observations[slot] = observation;
                _observedFrames[slot] = frame;
            }

            private static bool IsEntryOnlyCondition(CharacterTestRuntimeAiLabCardConditionFact fact)
            {
                switch (fact)
                {
                    case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                    case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                    case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                    case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                        return true;
                    default:
                        return false;
                }
            }

            private static bool CompareBool(
                bool actual,
                in CharacterTestRuntimeAiLabCompiledCondition condition)
            {
                return condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals
                    ? actual != condition.BoolValue
                    : actual == condition.BoolValue;
            }

            private static bool CompareFloat(
                float actual,
                in CharacterTestRuntimeAiLabCompiledCondition condition)
            {
                switch (condition.Operator)
                {
                    case CharacterTestRuntimeAiLabCardConditionOperator.Equals:
                        return Mathf.Approximately(actual, condition.Value);
                    case CharacterTestRuntimeAiLabCardConditionOperator.NotEquals:
                        return !Mathf.Approximately(actual, condition.Value);
                    case CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual:
                        return actual <= condition.Value;
                    case CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual:
                        return actual >= condition.Value;
                    case CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan:
                        return actual > condition.Value;
                    case CharacterTestRuntimeAiLabCardConditionOperator.Between:
                        return actual >= condition.Value && actual <= condition.MaxValue;
                    default:
                        return false;
                }
            }

            private struct ParameterFactHistory
            {
                public bool Initialized;
                public CharacterTestRuntimeAiLabDecisionTraceValue Value;
                public CharacterTestRuntimeAiLabDecisionTraceValue PreviousValue;
                public int LastChangedFrame;
            }
        }
    }
}
