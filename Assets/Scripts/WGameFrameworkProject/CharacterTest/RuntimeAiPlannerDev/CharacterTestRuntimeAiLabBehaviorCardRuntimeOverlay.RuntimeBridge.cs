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
        internal sealed partial class RuntimeBridge :
            ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime,
            ICharacterTestRuntimeAiLabBehaviorRecipeV5Runtime,
            ICharacterTestRuntimeAiLabBehaviorCardLogicTraceSink
        {
            private readonly int _entityId;
            private readonly int _bindingNumericId;
            private readonly IReadOnlyList<CharacterTestCharacterEntity> _entities;
            private readonly ICharacterTestAiNavigationService _navigationService;
            private readonly CharacterTestPerceptionFrameCache _perceptionFrameCache;
            private CharacterTestCharacterEntity _entity;
            private CharacterTestRuntimeAiPlannerFactState _facts;
            private CharacterTestCharacterActionRuntime _actionRuntime;
            private AbilityRuntimeModule _abilityRuntime;
            private RuntimeFrame _frame;
            private CharacterTestRuntimeAiLabCompiledLogicStage _stage;
            private CharacterTestRuntimeAiLabLogicActionState _actionState;
            private bool _cancelRequested;
            private bool _clearControlPending;
            private bool _frameScopedControlCommandSubmitted;
            private int _enteredFrame;
            private readonly CharacterTestRuntimeAiLabCompiledLogicProgram _program;
            private readonly IReadOnlyList<int> _usedActionIds;
            private readonly IReadOnlyList<int> _configuredCooldownActionIds;
            internal CharacterTestActionUseResult SystemActionUseResult { get; private set; }
            private readonly EntryActionHandshake _entryHandshake = new EntryActionHandshake();
            private readonly RuntimeDecisionTraceState _decisionTrace;
            private readonly LogicFactRuntime _logicFacts;
            private CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState _orchestrationState;
            private CharacterTestRuntimeAiLabCompiledLogicStage _orchestrationTargetStage;
            private CharacterTestBehaviorRecipeOrchestrationOperation _orchestrationOperation;
            private int _orchestrationResourceId;
            private bool _orchestrationCommandPending;
            private bool _orchestrationCommandSubmitted;
            private bool _orchestrationRejectAfterCleanup;
            private long _orchestrationPreviousInstanceId;
            private MxFramework.CharacterAction.CharacterActionId _orchestrationExpectedActionId;
            private CharacterTestActionRejection _orchestrationPreviousRejectReason;
            private string _orchestrationFailureText = string.Empty;
            private int _orchestrationFailureReasonId;
            private int _orchestrationFailureDetailId;
            private CharacterTestActionUseFailure _orchestrationFailureDetails;

            private bool _commandPending
            {
                get => _entryHandshake.CommandPending;
                set => _entryHandshake.CommandPending = value;
            }
            private bool _commandRejected
            {
                get => _entryHandshake.CommandRejected;
                set => _entryHandshake.CommandRejected = value;
            }
            private bool _actionObserved
            {
                get => _entryHandshake.ActionObserved;
                set => _entryHandshake.ActionObserved = value;
            }
            private bool _actionStartedThisFrame
            {
                get => _entryHandshake.ActionStartedThisFrame;
                set => _entryHandshake.ActionStartedThisFrame = value;
            }
            private bool _adoptEntryStagePending
            {
                get => _entryHandshake.AdoptEntryStagePending;
                set => _entryHandshake.AdoptEntryStagePending = value;
            }
            private long _entryTransitionPreviousInstanceId
            {
                get => _entryHandshake.PreviousInstanceId;
                set => _entryHandshake.PreviousInstanceId = value;
            }
            private long _observedActionInstanceId
            {
                get => _entryHandshake.ObservedInstanceId;
                set => _entryHandshake.ObservedInstanceId = value;
            }
            private CharacterTestRuntimeAiLabCandidateEvaluationTrace[] _logicCandidates =>
                _decisionTrace.LogicCandidates;
            private string[] _detectorCandidateStableIds => _decisionTrace.DetectorCandidateStableIds;
            private string[] _stageCandidateStableIds => _decisionTrace.StageCandidateStableIds;
            private string[] _stageMainActionStableIds => _decisionTrace.StageMainActionStableIds;
            private bool[] _stageHasMainAction => _decisionTrace.StageHasMainAction;
            private string[] _transitionCandidateStableIds => _decisionTrace.TransitionCandidateStableIds;
            private int _logicCandidateCount
            {
                get => _decisionTrace.LogicCandidateCount;
                set => _decisionTrace.LogicCandidateCount = value;
            }
            private int _activeLogicCandidateIndex
            {
                get => _decisionTrace.ActiveLogicCandidateIndex;
                set => _decisionTrace.ActiveLogicCandidateIndex = value;
            }
            private bool _logicCandidateOverflowed
            {
                get => _decisionTrace.LogicCandidateOverflowed;
                set => _decisionTrace.LogicCandidateOverflowed = value;
            }
            private CharacterTestRuntimeAiLabDecisionTraceOverflowRegion _logicCandidateOverflowRegion
            {
                get => _decisionTrace.LogicCandidateOverflowRegion;
                set => _decisionTrace.LogicCandidateOverflowRegion = value;
            }
            private int _logicCandidateOverflowActualCount
            {
                get => _decisionTrace.LogicCandidateOverflowActualCount;
                set => _decisionTrace.LogicCandidateOverflowActualCount = value;
            }

            public ulong FactReadMask => _logicFacts.FactReadMask;
            public ulong CardArbitrationFactReadMask => _logicFacts.CardArbitrationFactReadMask;
            public ulong LogicConditionFactReadMask => _logicFacts.LogicConditionFactReadMask;
            public int LogicCandidateCount => _logicCandidateCount;
            public int LogicCandidateCapacity => _logicCandidates.Length;
            public bool LogicCandidateOverflowed => _logicCandidateOverflowed;
            public CharacterTestRuntimeAiLabDecisionTraceOverflowRegion LogicCandidateOverflowRegion =>
                _logicCandidateOverflowRegion;
            public int LogicCandidateOverflowActualCount => _logicCandidateOverflowActualCount;
            public CharacterTestRuntimeAiLabCandidateEvaluationTrace GetLogicCandidate(int index) =>
                index >= 0 && index < _logicCandidateCount ? _logicCandidates[index] : default;
            public CharacterTestRuntimeAiLabRuntimeIntentKind LastIntentKind { get; private set; }
            public CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind RuntimeEffectKind
            {
                get
                {
                    if (_commandRejected || _actionState == CharacterTestRuntimeAiLabLogicActionState.Failed)
                        return CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Failed;
                    if (_actionStartedThisFrame)
                        return CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Started;
                    if (_actionObserved && _cancelRequested
                        && _actionState == CharacterTestRuntimeAiLabLogicActionState.Completed)
                        return CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Cancelled;
                    if (_actionObserved && _actionState == CharacterTestRuntimeAiLabLogicActionState.Running)
                        return CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running;
                    if (_actionObserved && _actionState == CharacterTestRuntimeAiLabLogicActionState.Completed)
                        return CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Completed;
                    return CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved;
                }
            }
            public int RuntimeEffectReasonCode
            {
                get
                {
                    switch (RuntimeEffectKind)
                    {
                        case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Started: return CharacterTestAiTraceReason.RUNTIME_ACTION_STARTED;
                        case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running: return CharacterTestAiTraceReason.RUNTIME_ACTION_RUNNING;
                        case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Completed: return CharacterTestAiTraceReason.RUNTIME_ACTION_COMPLETED;
                        case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Cancelled: return CharacterTestAiTraceReason.RUNTIME_ACTION_CANCELLED;
                        case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Failed:
                            return (_entity?.Action.LastRejectReasonCode ?? 0) != 0
                                ? _entity.Action.LastRejectReasonCode
                                : CharacterTestAiTraceReason.RUNTIME_ACTION_FAILED;
                        default: return 0;
                    }
                }
            }

            public RuntimeBridge(
                int entityId,
                string bindingStableId,
                CharacterTestRuntimeAiLabCompiledLogicProgram program,
                IReadOnlyList<CharacterTestCharacterEntity> entities,
                ICharacterTestAiNavigationService navigationService,
                CharacterTestPerceptionFrameCache perceptionFrameCache,
                CharacterTestCharacterRuntimeModule characterRuntime = null,
                CharacterTestRuntimeAiPlannerAttackOpportunityAdapter attackOpportunity = null, int bindingNumericId = 0)
            {
                _entityId = entityId;
                _bindingNumericId = bindingNumericId;
                _entities = entities;
                _navigationService = navigationService;
                _perceptionFrameCache = perceptionFrameCache;
                _program = program;
                _usedActionIds = CharacterTestBehaviorExecutionUsage.Analyze(program).ActionIds;
                _configuredCooldownActionIds = CharacterTestTacticalFrozenSource.CaptureCooldownActionIds(program, _usedActionIds);
                _executionCharacterRuntime = characterRuntime;
                _executionAttackOpportunity = attackOpportunity;
                if (program.Execution != null) characterRuntime?.RegisterBehaviorReceiptObserver(this);
                _decisionTrace = new RuntimeDecisionTraceState(bindingStableId, program);
                _logicFacts = new LogicFactRuntime(entityId, bindingStableId, program, _decisionTrace, bindingNumericId);
                InitializeExecutionObservation();
            }

            public CharacterTestRuntimeAiLabLogicActionState PrimaryActionState => _program.Execution != null ? ExecutionPrimaryActionState : _adoptEntryStagePending
                ? CharacterTestRuntimeAiLabLogicActionState.None
                : _actionState;
            public bool EntryCommandPending => _commandPending;
            public bool EntryActionObserved => _actionObserved;
            public long PrimaryActionInstanceId => _observedActionInstanceId;
            public int PrimaryActionSignalReasonCode => _entity?.Action.LastRejectReasonCode ?? 0;
            public bool PrimaryActionRejected => _commandRejected;
            public CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState OrchestrationState =>
                _orchestrationState;
            public int OrchestrationFailureReasonCode => _orchestrationFailureReasonId;

            public string MainActionStableId(int stageIndex) => _program.Execution != null ? ExecutionActionStableId() :
                stageIndex >= 0 && stageIndex < _stageMainActionStableIds.Length
                    ? _stageMainActionStableIds[stageIndex]
                    : string.Empty;
            public RuntimeTraceIdentity MainActionIdentity(int stageIndex)
            {
                if (_program.Execution != null)
                {
                    if (_actionRuntime != null && _actionRuntime.TryGetActiveActionPresentationState(new CharacterTestCharacterEntityId(_entityId), out _, out var action, out _, out _))
                        return CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.CharacterActionKind, action.Value);
                    return default;
                }
                if (stageIndex < 0 || stageIndex >= _program.Stages.Count) return default;
                var stage = _program.Stages[stageIndex];
                if (stage.CharacterActionId.IsValid) return CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.CharacterActionKind, stage.CharacterActionId.Value);
                if (stage.AbilityId > 0) return CharacterTestAiEvidenceIds.Definition(CharacterTestAiEvidenceIds.AbilityKind, stage.AbilityId);
                return stage.OwnsPrimaryAction ? CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.MainActionKind, stageIndex, _bindingNumericId) : default;
            }
            public int ParameterFactSlotCount => _logicFacts.ParameterFactSlotCount;

            public bool TryGetParameterFact(
                int slot,
                int frame,
                out CharacterTestRuntimeAiLabFactObservationTrace observation)
            {
                return _logicFacts.TryGetParameterFact(slot, frame, out observation);
            }

            public void RestoreParameterizedFactHistory(
                IReadOnlyList<CharacterTestRuntimeAiLabFactObservationTrace> source)
            {
                _logicFacts.RestoreParameterizedFactHistory(source);
            }

            public void SetFrame(
                CharacterTestCharacterEntity entity,
                CharacterTestRuntimeAiPlannerFactState facts,
                RuntimeFrame frame,
                CharacterTestCharacterActionRuntime actionRuntime,
                AbilityRuntimeModule abilityRuntime)
            {
                _entity = entity;
                _facts = facts;
                _frame = frame;
                _actionRuntime = actionRuntime;
                _abilityRuntime = abilityRuntime;
                _logicFacts.SetFrame(facts, frame, actionRuntime, abilityRuntime, entity);
                _decisionTrace.BeginFrame();
                _entryHandshake.BeginFrame();
                LastIntentKind = CharacterTestRuntimeAiLabRuntimeIntentKind.None;
            }

            public void Reset()
            {
                ResetExecutionRuntime();
                _stage = null;
                _facts = null;
                _actionState = CharacterTestRuntimeAiLabLogicActionState.None;
                _entryHandshake.Reset();
                _cancelRequested = false;
                _clearControlPending = false;
                _frameScopedControlCommandSubmitted = false;
                _enteredFrame = 0;
                CompleteOrchestration();
                _orchestrationFailureText = string.Empty;
                _orchestrationFailureReasonId = 0;
                _orchestrationFailureDetailId = 0;
                _orchestrationFailureDetails = default;
                _logicFacts.Reset();
                _decisionTrace.BeginFrame();
                LastIntentKind = CharacterTestRuntimeAiLabRuntimeIntentKind.None;
            }

            internal bool MeetsSystemActionRequirements()
            {
                SystemActionUseResult = default;
                if (!MeetsConfiguredActionCooldowns()) return false;
                if (_usedActionIds.Count == 0) return true;
                // Native multi-action cards keep their existing admission contract. Tactical cards
                // can combine attack and step actions; query each dependency without reserving costs.
                if (_usedActionIds.Count > 1 && _program.TacticalSource == null) return true;
                for (int i = 0; i < _usedActionIds.Count; i++)
                {
                    if (_executionCharacterRuntime == null)
                    {
                        if (_actionRuntime?.Registry != null && _actionRuntime.Registry.TryGetAction(new CharacterActionId(_usedActionIds[i]), out var action)
                            && action.Requirements.Length == 0) continue;
                        SystemActionUseResult = new CharacterTestActionUseResult(CharacterTestActionUseStatus.ActionMissing); return false;
                    }
                    SystemActionUseResult = _executionCharacterRuntime.ActionUseAuthority.Evaluate(_entity, _usedActionIds[i], _frame, _facts);
                    if (!SystemActionUseResult.Available) return false;
                }
                return true;
            }

            internal bool MeetsConfiguredActionCooldowns()
            {
                SystemActionUseResult = EvaluateConfiguredActionCooldowns(_configuredCooldownActionIds,
                    _executionCharacterRuntime?.ActionUseAuthority, _executionCharacterRuntime?.ActionRuntime.Registry,
                    _entityId, _frame);
                return SystemActionUseResult.Available;
            }

            internal static CharacterTestActionUseResult EvaluateConfiguredActionCooldowns(IReadOnlyList<int> actionIds,
                CharacterTestActionUseAuthority authority, CharacterActionConfigRegistry registry, int entityId, RuntimeFrame frame)
            {
                if (actionIds.Count == 0) return new CharacterTestActionUseResult(CharacterTestActionUseStatus.Available);
                return authority == null ? new CharacterTestActionUseResult(CharacterTestActionUseStatus.ActionMissing)
                    : authority.EvaluateCooldowns(entityId, actionIds, registry, frame);
            }

            public bool CanActivate(CharacterTestRuntimeAiLabCompiledLogicProgram program)
            {
                if (program?.Execution != null)
                    return CharacterTestBehaviorExecutionEvaluator.EvaluateCondition(program.Execution, program.Execution.ActivationCondition, this)
                        && CharacterTestBehaviorExecutionEvaluator.EvaluateCondition(program.Execution,
                            program.Execution.Timelines[program.Execution.EntryTimeline].EntryCondition, this);
                if (program?.RecipeV6 != null)
                {
                    if (!EvaluateV6Condition(program.RecipeV6.ActivationCondition))
                        return false;
                    int timeline = program.RecipeV6.EntryTimelineIndex;
                    return timeline >= 0
                        && timeline < program.RecipeV6.Timelines.Count
                        && EvaluateV6Condition(program.RecipeV6.Timelines[timeline].EntryCondition);
                }
                if (!_logicFacts.CanActivate(program))
                    return false;
                int entryStageIndex = program?.EntryStageIndex ?? -1;
                return entryStageIndex < 0
                    || entryStageIndex >= program.Stages.Count
                    || IsMovementStageEligible(program.Stages[entryStageIndex]);
            }

            public bool CanRemainActive(CharacterTestRuntimeAiLabCompiledLogicProgram program)
            {
                if (program?.Execution != null) return true;
                if (program?.RecipeV6 != null)
                    return IsMovementStageEligible(_stage);
                return _logicFacts.CanRemainActive(program)
                    && IsMovementStageEligible(_stage);
            }

            public bool Evaluate(in CharacterTestRuntimeAiLabCompiledCondition condition)
            {
                // A command receipt may enter another timeline in this same world frame.
                // Resolve its newly reachable dependency here, inside the execution context only.
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange
                    && _facts != null && !_facts.PrimaryAttackFactObserved)
                {
                    var target = CurrentExecutionTarget();
                    bool inRange = _entity != null && target != null && _executionAttackOpportunity != null
                        && _executionAttackOpportunity.TryPreviewPlannerPrimaryAttack(_entity, target, _frame, out var result)
                        && result.Status == CharacterTestAttackOpportunityStatus.Valid
                        && result.Level == CharacterTestAttackOpportunityLevel.CurrentPose && result.CanHitFromCurrentPose;
                    _facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange, inRange);
                    _facts.SetPrimaryAttackFactObserved(true);
                }
                return _logicFacts.Evaluate(condition);
            }

            private bool EvaluateV6Condition(CharacterTestRuntimeAiLabCompiledRecipeV6Condition node)
            {
                if (node == null)
                    return true;
                switch (node.Kind)
                {
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate:
                        return node.Predicate.HasValue && Evaluate(node.Predicate.Value);
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not:
                        return node.Children.Count == 1 && !EvaluateV6Condition(node.Children[0]);
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any:
                        for (int i = 0; i < node.Children.Count; i++)
                            if (EvaluateV6Condition(node.Children[i])) return true;
                        return false;
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All:
                        for (int i = 0; i < node.Children.Count; i++)
                            if (!EvaluateV6Condition(node.Children[i])) return false;
                        return node.Children.Count > 0;
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope:
                        return node.Children.Count == 1
                            && CharacterTestRuntimeAiLabBehaviorRecipeSubjectAvailability.IsAvailable(
                                node.Subject,
                                this)
                            && EvaluateV6Condition(node.Children[0]);
                    default:
                        return false;
                }
            }

            public void SetFactTraceLayer(CharacterTestRuntimeAiLabDecisionTraceLayer layer)
            {
                _logicFacts.SetTraceLayer(layer);
            }

        }
    }
}
