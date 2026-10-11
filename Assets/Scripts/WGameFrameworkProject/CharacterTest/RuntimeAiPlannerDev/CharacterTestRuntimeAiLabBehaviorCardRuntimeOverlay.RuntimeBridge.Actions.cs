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
        internal sealed partial class RuntimeBridge
        {
            public bool TryInterruptPrimaryAction()
            {
                if (_program.Execution != null) return InterruptExecutionAction();
                if (_stage == null || _actionState != CharacterTestRuntimeAiLabLogicActionState.Running)
                    return true;
                if (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    || IsLegacyMovement(_stage))
                {
                    _cancelRequested = true;
                    _commandPending = true;
                }
                return true;
            }

            public void RefreshActionState()
            {
                if (_program.Execution != null) { RefreshExecutionActionState(); return; }
                RefreshOrchestrationState();
                if (_stage == null)
                    return;
                if (_cancelRequested
                    && _commandRejected
                    && (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        || IsLegacyMovement(_stage)))
                {
                    _actionState = CharacterTestRuntimeAiLabLogicActionState.Failed;
                    return;
                }
                int currentFrame = ClampFrame(_frame.Value);
                if (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)
                {
                    if (currentFrame - _enteredFrame >= Math.Max(0, _stage.WaitFrames))
                        _actionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
                    return;
                }
                if (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    || IsLegacyMovement(_stage))
                {
                    if (_cancelRequested && !_commandPending)
                        _actionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
                    return;
                }

                CharacterActionId activeActionId = _entity?.Action.ActiveActionId ?? default;
                long activeInstanceId = 0L;
                CharacterActionId runtimeActionId = default;
                bool running = _actionRuntime != null
                    && _actionRuntime.TryGetActiveActionPresentationState(
                        new CharacterTestCharacterEntityId(_entityId),
                        out activeInstanceId,
                        out runtimeActionId,
                        out _,
                        out _);
                if (running)
                    activeActionId = runtimeActionId;
                else
                    running = activeActionId.IsValid;
                bool expected = _stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    ? activeActionId == _stage.CharacterActionId
                    : running;
                if (running && expected)
                {
                    if (!_actionObserved)
                    {
                        _actionStartedThisFrame = true;
                        _observedActionInstanceId = activeInstanceId;
                    }
                    else if (_observedActionInstanceId == 0L)
                    {
                        // Restored snapshots intentionally keep the existing schema. Rebind the
                        // owned instance from the authoritative Gateway on the first live tick.
                        _observedActionInstanceId = activeInstanceId;
                    }
                    else if (activeInstanceId > 0L
                        && activeInstanceId != _observedActionInstanceId)
                    {
                        // A different instance, even with the same action id, belongs to a later
                        // request and must not extend this behavior card's ownership.
                        _actionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
                        return;
                    }
                    _actionObserved = true;
                    _actionState = CharacterTestRuntimeAiLabLogicActionState.Running;
                    return;
                }
                if (_actionObserved || (_cancelRequested && !running))
                {
                    _actionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
                    return;
                }
                if (_commandRejected || (!string.IsNullOrEmpty(_entity?.Action.LastRejectReason) && !_commandPending))
                    _actionState = CharacterTestRuntimeAiLabLogicActionState.Failed;
            }

            public bool TryBuildIntent(out CharacterTestRuntimeAiLabRuntimeIntent intent)
            {
                if (_program.Execution != null) return TryBuildExecutionIntent(out intent);
                intent = default;
                if (TryBuildOrchestrationIntent(out intent))
                    return true;
                if (_stage == null)
                    return false;

                if (_cancelRequested)
                {
                    if (!_commandPending)
                        return false;
                    _commandPending = false;
                    if (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        || IsLegacyMovement(_stage))
                    {
                        intent = new CharacterTestRuntimeAiLabRuntimeIntent(
                            CharacterTestRuntimeAiLabRuntimeIntentKind.Control,
                            Vector2.zero);
                    }
                    LastIntentKind = intent.Kind;
                    return true;
                }

                // A movement stage can hand control to Logic, Wait, CharacterAction, or another
                // non-movement stage before this bridge gets its next chance to emit an intent.
                // Clear the previously submitted movement independently of the new stage kind so
                // a stale non-zero SetControlIntent cannot keep driving the character afterward.
                if (_clearControlPending)
                {
                    _clearControlPending = false;
                    intent = new CharacterTestRuntimeAiLabRuntimeIntent(
                        CharacterTestRuntimeAiLabRuntimeIntentKind.Control,
                        Vector2.zero);
                    LastIntentKind = intent.Kind;
                    return true;
                }

                if (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
                {
                    if (_stage.FrameScopedControlOperation)
                    {
                        if (!_commandPending || _frameScopedControlCommandSubmitted)
                            return false;
                        _commandPending = false;
                        _frameScopedControlCommandSubmitted = true;
                    }
                    intent = new CharacterTestRuntimeAiLabRuntimeIntent(
                        CharacterTestRuntimeAiLabRuntimeIntentKind.Control,
                        ResolveMove(),
                        movementSpeedMultiplier: _stage.MoveSpeedMultiplier);
                    LastIntentKind = intent.Kind;
                    return true;
                }
                if (IsLegacyMovement(_stage))
                {
                    intent = new CharacterTestRuntimeAiLabRuntimeIntent(
                        CharacterTestRuntimeAiLabRuntimeIntentKind.Control,
                        ResolveMove(),
                        movementSpeedMultiplier: _stage.MoveSpeedMultiplier);
                    LastIntentKind = intent.Kind;
                    return true;
                }
                if (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)
                    return false;
                if (!_commandPending)
                    return false;
                _commandPending = false;

                if (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility)
                {
                    intent = new CharacterTestRuntimeAiLabRuntimeIntent(
                        CharacterTestRuntimeAiLabRuntimeIntentKind.CastAbility,
                        Vector2.zero,
                        abilityId: _stage.AbilityId);
                    LastIntentKind = intent.Kind;
                    return true;
                }
                if (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction)
                {
                    if (_actionRuntime?.Registry == null
                        || !_actionRuntime.Registry.TryGetAction(_stage.CharacterActionId, out CharacterActionConfig action))
                    {
                        _commandRejected = true;
                        return false;
                    }
                    intent = new CharacterTestRuntimeAiLabRuntimeIntent(
                        CharacterTestRuntimeAiLabRuntimeIntentKind.BehaviorRecipeOrchestration,
                        Vector2.zero,
                        orchestrationOperation: CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction,
                        orchestrationResourceId: action.ActionId.Value);
                    LastIntentKind = intent.Kind;
                    return true;
                }
                _commandRejected = true;
                return false;
            }

            public bool TryBeginTimelineJump(CharacterTestRuntimeAiLabCompiledLogicStage target)
            {
                if (target == null
                    || _orchestrationState !=
                        CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None)
                {
                    return false;
                }
                for (int i = 0; i < target.EntryConditions.Count; i++)
                {
                    if (!Evaluate(target.EntryConditions[i]))
                        return BeginRejectedOrchestrationCleanup(CharacterTestBehaviorFailureCode.EntryConditionRejected);
                }

                CharacterTestBehaviorRecipeOrchestrationOperation operation;
                int resourceId = 0;
                CharacterActionId expectedActionId = CharacterActionId.None;
                switch (target.Kind)
                {
                    case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                        if (_actionRuntime?.Registry == null
                            || !_actionRuntime.Registry.TryGetAction(
                                target.CharacterActionId,
                                out CharacterActionConfig action))
                        {
                            return BeginRejectedOrchestrationCleanup(
                                CharacterTestBehaviorFailureCode.CharacterActionUnknown);
                        }
                        operation = CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction;
                        resourceId = action.ActionId.Value;
                        expectedActionId = action.ActionId;
                        break;

                    case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                        if (_abilityRuntime == null
                            || !_abilityRuntime.Definitions.TryGet(
                                target.AbilityId,
                                out AbilityDefinitionConfig ability)
                            || !_abilityRuntime.TryEvaluate(
                                _entityId,
                                ability,
                                _frame,
                                out AbilityActivationResult abilityResult))
                        {
                            return BeginRejectedOrchestrationCleanup(
                                _abilityRuntime == null
                                    ? CharacterTestBehaviorFailureCode.AbilityRuntimeMissing
                                    : CharacterTestBehaviorFailureCode.AbilityRejected);
                        }
                        expectedActionId = abilityResult.ActionBinding?.ActionId ?? CharacterActionId.None;
                        if (!expectedActionId.IsValid
                            || _actionRuntime?.Registry == null
                            || !_actionRuntime.Registry.TryGetAction(expectedActionId, out _))
                        {
                            return BeginRejectedOrchestrationCleanup(
                                CharacterTestBehaviorFailureCode.AbilityActionUnknown);
                        }
                        operation = CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceAbility;
                        resourceId = target.AbilityId;
                        break;

                    case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                        if (!IsMovementStageEligible(target))
                            return BeginRejectedOrchestrationCleanup(CharacterTestBehaviorFailureCode.MoveTargetInvalid);
                        operation = CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForTimelineJump;
                        break;

                    case CharacterTestRuntimeAiLabCardStepKind.Wait:
                        if (target.WaitFrames <= 0)
                            return BeginRejectedOrchestrationCleanup(CharacterTestBehaviorFailureCode.WaitInvalid);
                        operation = CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForTimelineJump;
                        break;

                    case CharacterTestRuntimeAiLabCardStepKind.Logic:
                        operation = CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForTimelineJump;
                        break;

                    default:
                        return BeginRejectedOrchestrationCleanup(CharacterTestBehaviorFailureCode.PrimaryActionInvalid);
                }

                BeginOrchestration(operation, resourceId, target, expectedActionId, rejectAfterCleanup: false);
                return true;
            }

            public bool TryBeginReturnToPlanner()
            {
                if (_orchestrationState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None)
                    return false;
                BeginOrchestration(
                    CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner,
                    resourceId: 0,
                    target: null,
                    expectedActionId: CharacterActionId.None,
                    rejectAfterCleanup: false);
                return true;
            }

            public void CompleteOrchestration()
            {
                _orchestrationState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None;
                _orchestrationTargetStage = null;
                _orchestrationOperation = default;
                _orchestrationResourceId = 0;
                _orchestrationCommandPending = false;
                _orchestrationCommandSubmitted = false;
                _orchestrationRejectAfterCleanup = false;
                _orchestrationPreviousInstanceId = 0L;
                _orchestrationExpectedActionId = CharacterActionId.None;
                _orchestrationPreviousRejectReason = default;
            }

            private void SetOrchestrationFailure(CharacterTestBehaviorFailureCode reason)
            {
                _orchestrationFailureReasonId = (int)reason;
                _orchestrationFailureDetailId = 0;
                _orchestrationFailureDetails = default;
                _orchestrationFailureText = CharacterTestBehaviorFailureCodes.Display(reason);
            }

            private void SetOrchestrationFailure(in CharacterTestActionRejection reason)
            {
                _orchestrationFailureReasonId = (int)CharacterTestBehaviorFailureCode.ActionOwnerDomain + reason.Code;
                _orchestrationFailureDetailId = reason.DetailId;
                _orchestrationFailureDetails = default;
                _orchestrationFailureText = null;
            }

            private bool BeginRejectedOrchestrationCleanup(CharacterTestBehaviorFailureCode reasonCode)
            {
                SetOrchestrationFailure(reasonCode);
                BeginOrchestration(
                    CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner,
                    resourceId: 0,
                    target: null,
                    expectedActionId: CharacterActionId.None,
                    rejectAfterCleanup: true);
                return true;
            }

            private void BeginOrchestration(
                CharacterTestBehaviorRecipeOrchestrationOperation operation,
                int resourceId,
                CharacterTestRuntimeAiLabCompiledLogicStage target,
                CharacterActionId expectedActionId,
                bool rejectAfterCleanup)
            {
                long activeInstanceId = 0L;
                _actionRuntime?.TryGetActiveActionPresentationState(
                    new CharacterTestCharacterEntityId(_entityId),
                    out activeInstanceId,
                    out _,
                    out _,
                    out _);
                _orchestrationTargetStage = target;
                _orchestrationOperation = operation;
                _orchestrationResourceId = resourceId;
                _orchestrationCommandPending = true;
                _orchestrationCommandSubmitted = false;
                _orchestrationRejectAfterCleanup = rejectAfterCleanup;
                _orchestrationPreviousInstanceId = activeInstanceId;
                _orchestrationExpectedActionId = expectedActionId;
                _orchestrationPreviousRejectReason = _entity?.Action.LastRejection ?? default;
                if (!rejectAfterCleanup)
                    SetOrchestrationFailure(CharacterTestBehaviorFailureCode.None);
                _orchestrationState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending;
            }

            private bool TryBuildOrchestrationIntent(out CharacterTestRuntimeAiLabRuntimeIntent intent)
            {
                intent = default;
                if (_orchestrationState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending
                    || !_orchestrationCommandPending)
                {
                    return false;
                }
                _orchestrationCommandPending = false;
                _orchestrationCommandSubmitted = true;
                intent = new CharacterTestRuntimeAiLabRuntimeIntent(
                    CharacterTestRuntimeAiLabRuntimeIntentKind.BehaviorRecipeOrchestration,
                    Vector2.zero,
                    orchestrationOperation: _orchestrationOperation,
                    orchestrationResourceId: _orchestrationResourceId);
                LastIntentKind = intent.Kind;
                return true;
            }

            private void RefreshOrchestrationState()
            {
                if (_orchestrationState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending
                    || !_orchestrationCommandSubmitted)
                {
                    return;
                }

                long activeInstanceId = 0L;
                CharacterActionId activeActionId = default;
                bool running = _actionRuntime != null
                    && _actionRuntime.TryGetActiveActionPresentationState(
                        new CharacterTestCharacterEntityId(_entityId),
                        out activeInstanceId,
                        out activeActionId,
                        out _,
                        out _);
                bool replacing = _orchestrationOperation ==
                        CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction
                    || _orchestrationOperation ==
                        CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceAbility;
                if (replacing)
                {
                    if (running
                        && activeInstanceId > 0L
                        && activeInstanceId != _orchestrationPreviousInstanceId
                        && activeActionId == _orchestrationExpectedActionId)
                    {
                        _orchestrationState =
                            CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded;
                        return;
                    }
                    CharacterTestActionRejection rejectReason = _entity?.Action.LastRejection ?? default;
                    if (!rejectReason.IsNone
                        && ((_program.Execution != null && _frame.Value > _executionSubmittedFrame)
                        || !rejectReason.Equals(_orchestrationPreviousRejectReason)))
                    {
                        if (_program.Execution != null)
                        {
                            SetOrchestrationFailure(rejectReason);
                            _orchestrationState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected;
                            return;
                        }
                        SetOrchestrationFailure(rejectReason);
                        BeginOrchestration(
                            CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner,
                            resourceId: 0,
                            target: null,
                            expectedActionId: CharacterActionId.None,
                            rejectAfterCleanup: true);
                    }
                    return;
                }

                if (!running || (_program.Execution != null && activeInstanceId != _orchestrationPreviousInstanceId))
                {
                    _orchestrationState = _orchestrationRejectAfterCleanup
                        ? CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected
                        : CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded;
                }
            }

            public bool TryBuildReleaseIntent(out CharacterTestRuntimeAiLabRuntimeIntent intent)
            {
                if (_program.Execution != null)
                {
                    SetExecutionControl(Vector2.zero, 1f, new CharacterTestBehaviorControlInput(1, 0, 0, ownerToken: _executionControl.OwnerToken, isRelease: true));
                    return TryBuildExecutionIntent(out intent);
                }
                if (_stage != null
                    && (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        || IsLegacyMovement(_stage)))
                {
                    intent = new CharacterTestRuntimeAiLabRuntimeIntent(
                        CharacterTestRuntimeAiLabRuntimeIntentKind.Control,
                        Vector2.zero);
                    LastIntentKind = intent.Kind;
                    return true;
                }
                intent = default;
                return false;
            }

            public bool IsCharacterActionEntry(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                return stage != null
                    && stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    && stage.OwnsPrimaryAction;
            }

            public bool ShouldIssueEntryTransition(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                return IsCharacterActionEntry(stage)
                    && (!ReferenceEquals(_stage, stage) || _commandPending);
            }

            public bool TryBuildEntryTransitionIntent(
                CharacterTestRuntimeAiLabCompiledLogicStage stage,
                out CharacterTestRuntimeAiLabRuntimeIntent intent)
            {
                intent = default;
                if (!IsCharacterActionEntry(stage))
                    return false;

                if (!ReferenceEquals(_stage, stage))
                {
                    long previousInstanceId = 0L;
                    _actionRuntime?.TryGetActiveActionPresentationState(
                        new CharacterTestCharacterEntityId(_entityId),
                        out previousInstanceId,
                        out _,
                        out _,
                        out _);
                    _entryTransitionPreviousInstanceId = previousInstanceId;
                    _stage = stage;
                    _enteredFrame = ClampFrame(_frame.Value);
                    _commandRejected = false;
                    _actionObserved = false;
                    _observedActionInstanceId = 0L;
                    _cancelRequested = false;
                    _commandPending = true;
                    _clearControlPending = false;
                    _adoptEntryStagePending = false;
                    _actionState = CharacterTestRuntimeAiLabLogicActionState.Running;
                }

                return TryBuildIntent(out intent);
            }

            public bool HasObservedEntryAction(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                if (!IsCharacterActionEntry(stage) || _actionRuntime == null)
                    return false;

                if (!_actionRuntime.TryGetActiveActionPresentationState(
                        new CharacterTestCharacterEntityId(_entityId),
                        out long currentInstanceId,
                        out CharacterActionId currentActionId,
                        out _,
                        out _)
                    || currentActionId != stage.CharacterActionId)
                {
                    return false;
                }

                return currentInstanceId > 0L
                    && currentInstanceId != _entryTransitionPreviousInstanceId;
            }

            public bool IsEntryTransitionRejected(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                if (!IsCharacterActionEntry(stage) || HasObservedEntryAction(stage))
                    return false;
                bool rejected = _commandRejected
                    || (!_commandPending && !string.IsNullOrEmpty(_entity?.Action.LastRejectReason));
                if (rejected)
                {
                    _commandRejected = true;
                    _actionState = CharacterTestRuntimeAiLabLogicActionState.Failed;
                }
                return rejected;
            }

            public void AdoptObservedEntryAction(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                if (!IsCharacterActionEntry(stage))
                    return;
                _stage = stage;
                _enteredFrame = ClampFrame(_frame.Value);
                _commandPending = false;
                _commandRejected = false;
                _actionObserved = true;
                long observedInstanceId = 0L;
                _actionRuntime?.TryGetActiveActionPresentationState(
                    new CharacterTestCharacterEntityId(_entityId),
                    out observedInstanceId,
                    out _,
                    out _,
                    out _);
                _observedActionInstanceId = observedInstanceId;
                _actionStartedThisFrame = true;
                _cancelRequested = false;
                _clearControlPending = false;
                _adoptEntryStagePending = true;
                _actionState = CharacterTestRuntimeAiLabLogicActionState.Running;
                _entryTransitionPreviousInstanceId = 0L;
            }

            public void RecordCommandResult(bool success)
            {
                if (_program.Execution != null)
                {
                    if (!success)
                    {
                        SetOrchestrationFailure(CharacterTestBehaviorFailureCode.CommandRejected);
                        if (_orchestrationCommandSubmitted)
                        {
                            _orchestrationState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected;
                            _orchestrationCommandPending = false;
                        }
                        else _commandRejected = true;
                    }
                    return;
                }
                if (_orchestrationState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending
                    && _orchestrationCommandSubmitted)
                {
                    if (!success)
                    {
                        if (_orchestrationOperation ==
                                CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction
                            || _orchestrationOperation ==
                                CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceAbility)
                        {
                            SetOrchestrationFailure(CharacterTestBehaviorFailureCode.OrchestrationCommandRejected);
                            BeginOrchestration(
                                CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner,
                                resourceId: 0,
                                target: null,
                                expectedActionId: CharacterActionId.None,
                                rejectAfterCleanup: true);
                        }
                        else
                        {
                            SetOrchestrationFailure(CharacterTestBehaviorFailureCode.OrchestrationTerminateRejected);
                            _orchestrationState =
                                CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected;
                        }
                    }
                    return;
                }
                if (!success)
                    _commandRejected = true;
                if (_frameScopedControlCommandSubmitted
                    && _stage?.FrameScopedControlOperation == true)
                {
                    _frameScopedControlCommandSubmitted = false;
                    // A frame-scoped movement command is sampled once, but the accepted control
                    // intent remains owned by this event until its duration edge or another
                    // orchestration boundary releases it. Clearing it on the following frame made
                    // MaxExecutionsPerPass=1 behave like a one-frame pulse and caused visible
                    // stop/start movement. The duration/timeline cancellation paths are the sole
                    // authority for submitting the matching zero-control intent.
                    _actionState = success
                        ? CharacterTestRuntimeAiLabLogicActionState.Running
                        : CharacterTestRuntimeAiLabLogicActionState.Failed;
                }
            }

            public static string BuildMainActionStableId(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                if (stage == null || !stage.OwnsPrimaryAction)
                    return string.Empty;
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction)
                    return stage.CharacterActionId.IsValid
                        ? "character-action:" + stage.CharacterActionId.Value
                        : string.Empty;
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility)
                    return "ability:" + stage.AbilityId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
                    return "movement:" + stage.MovementStrategy;
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)
                    return "action:wait";
                return "action:legacy:" + stage.LegacyActionKind;
            }

            public CharacterTestRuntimeAiLabLogicAgentStateSnapshot CreateStateSnapshot(
                int entityId,
                int activeBindingIndex,
                long programSemanticHash,
                CharacterTestRuntimeAiLabLogicEvaluatorSnapshot evaluator,
                CharacterTestRuntimeAiLabCompiledLogicProgram program,
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot target = null)
            {
                target ??= new CharacterTestRuntimeAiLabLogicAgentStateSnapshot();
                if (program.Execution != null)
                {
                    var state = evaluator.ExecutionState;
                    target.EntityId = entityId;
                    target.ActiveBindingIndex = activeBindingIndex;
                    target.ProgramSemanticHash = programSemanticHash;
                    target.RuntimeState = (int)evaluator.State;
                    target.CurrentStageIndex = state.Timeline;
                    target.CurrentTimelineIndex = state.Timeline;
                    target.CardStartedFrame = state.CardStartedFrame;
                    target.StageEnteredFrame = state.TimelineEnteredFrame;
                    target.TimelineEnteredFrame = state.TimelineEnteredFrame;
                    target.LocalFrame = state.LocalFrame;
                    target.LocalPlayheadFrame = state.LocalFrame;
                    target.LastSampledPlayheadFrame = state.LastSampledFrame;
                    target.LastEvaluatedRuntimeFrame = state.LastWorldFrame;
                    target.TimelinePassGeneration = state.PassGeneration;
                    target.ActiveWaitEventIndex = state.WaitEvent;
                    target.ActorOwnerEventIndex = state.ActionOwner;
                    target.PendingTimelineIndex = state.JumpTarget;
                    target.PendingEventIndex = state.PendingAction >= 0 ? state.PendingAction : state.FlowEvent;
                    target.LastTriggeredEventIndex = state.LastTriggeredEvent;
                    target.EventExecutionCounts = state.Counts;
                    target.EventLastExecutionFrames = state.LastExecutions;
                    target.EventResults = state.Results;
                    target.LoopRepeatCounts = state.LoopRepeats;
                    target.PrimaryActionInstanceId = state.ActionInstanceId;
                    target.PrimaryActionState = (int)PrimaryActionState;
                    target.PrimaryActionStageIndex = state.Timeline;
                    target.PrimaryActionTimelineIndex = state.Timeline;
                    target.ExecutionState = state;
                    target.ExecutionRuntimeState = CreateExecutionRuntimeSnapshot(target.ExecutionRuntimeState);
                    return target;
                }
                int pendingTimelineIndex = -1;
                int pendingEventIndex = -1;
                if (evaluator.PendingTransitionIndex >= 0
                    && evaluator.PendingTransitionIndex < program.Transitions.Count)
                {
                    CharacterTestRuntimeAiLabCompiledLogicTransition transition =
                        program.Transitions[evaluator.PendingTransitionIndex];
                    pendingTimelineIndex = transition.TargetStageIndex;
                    if (transition.DetectorIndex >= 0
                        && transition.DetectorIndex < program.Detectors.Count)
                    {
                        pendingEventIndex = transition.DetectorIndex;
                    }
                }
                if (program.RecipeV6 != null)
                {
                    pendingTimelineIndex = evaluator.PendingTimelineIndex;
                    if (evaluator.PendingEventIndex >= 0
                        && evaluator.PendingEventIndex < program.RecipeV6.Events.Count)
                        pendingEventIndex = evaluator.PendingEventIndex;
                }
                int primaryTimelineIndex = _stage != null && _stage.OwnsPrimaryAction
                    ? FindStageIndex(program, _stage)
                    : -1;
                target.EntityId = entityId;
                    target.ActiveBindingIndex = activeBindingIndex;
                    target.ProgramSemanticHash = programSemanticHash;
                    target.RuntimeState = (int)evaluator.State;
                    target.CurrentStageIndex = evaluator.CurrentStageIndex;
                    target.CurrentTimelineIndex = evaluator.CurrentTimelineIndex;
                    target.CardStartedFrame = evaluator.CardStartedFrame;
                    target.StageEnteredFrame = evaluator.StageEnteredFrame;
                    target.TimelineEnteredFrame = evaluator.TimelineEnteredFrame;
                    target.LocalFrame = evaluator.LocalFrame;
                    target.PendingTransitionIndex = evaluator.PendingTransitionIndex;
                    target.PendingTimelineIndex = pendingTimelineIndex;
                    target.PendingEventIndex = pendingEventIndex;
                    target.LastTriggeredEventIndex = evaluator.LastTriggeredEventIndex;
                    target.DetectorArmed = CopyCaptureArray(evaluator.DetectorArmed, target.DetectorArmed);
                    target.TriggerArmed = CopyCaptureArray(evaluator.DetectorArmed, target.TriggerArmed);
                    target.TriggerFired = CopyCaptureArray(evaluator.DetectorFired, target.TriggerFired);
                    target.PrimaryActionState = (int)_actionState;
                    target.PrimaryActionStageIndex = primaryTimelineIndex;
                    target.PrimaryActionTimelineIndex = primaryTimelineIndex;
                    target.PrimaryActionInstanceId = _observedActionInstanceId;
                    target.OrchestrationState = (int)_orchestrationState;
                    target.OrchestrationOperation = (int)_orchestrationOperation;
                    target.OrchestrationTargetTimelineIndex = FindStageIndex(program, _orchestrationTargetStage);
                    target.OrchestrationResourceId = _orchestrationResourceId;
                    target.OrchestrationCommandPending = _orchestrationCommandPending;
                    target.OrchestrationCommandSubmitted = _orchestrationCommandSubmitted;
                    target.OrchestrationRejectAfterCleanup = _orchestrationRejectAfterCleanup;
                    target.OrchestrationPreviousInstanceId = _orchestrationPreviousInstanceId;
                    target.OrchestrationFailureReasonId = _orchestrationFailureReasonId;
                    target.OrchestrationFailureDetailId = _orchestrationFailureDetailId;
                    target.OrchestrationFailureDetails = _orchestrationFailureDetails;
                    target.CommandPending = _commandPending;
                    target.CommandRejected = _commandRejected;
                    target.ActionObserved = _actionObserved;
                    target.CancelRequested = _cancelRequested;
                    target.ClearControlPending = _clearControlPending;
                    target.ActionEnteredFrame = _enteredFrame;
                    target.EventExecutionCounts = CopyCaptureArray(evaluator.EventExecutionCounts, target.EventExecutionCounts);
                    target.EventLastExecutionFrames = CopyCaptureArray(evaluator.EventLastExecutionFrames, target.EventLastExecutionFrames);
                    target.EventResults = CopyCaptureArray(evaluator.EventResults, target.EventResults);
                    target.PreviousEventResults = CopyCaptureArray(evaluator.PreviousEventResults, target.PreviousEventResults);
                    target.ActorOwnerEventIndex = evaluator.ActorOwnerEventIndex;
                    target.PendingV6Operation = evaluator.PendingV6Operation;
                    target.LastEvaluatedRuntimeFrame = evaluator.LastEvaluatedFrame;
                    target.LocalPlayheadFrame = evaluator.LocalPlayheadFrame;
                    target.TimelinePassGeneration = evaluator.TimelinePassGeneration;
                    target.LoopRepeatCounts = CopyCaptureArray(evaluator.LoopRepeatCounts, target.LoopRepeatCounts);
                    target.ActiveWaitEventIndex = evaluator.ActiveWaitEventIndex;
                    target.PendingPlayheadTargetFrame = evaluator.PendingPlayheadTargetFrame;
                    target.PendingTimeControlEventIndex = evaluator.PendingTimeControlEventIndex;
                    target.WaitingForInstantOwnerAtTimelineEnd = evaluator.WaitingForInstantOwnerAtTimelineEnd;
                    target.LastSampledPlayheadFrame = evaluator.LastSampledPlayheadFrame;
                    return target;
            }
            private static T[] CopyCaptureArray<T>(T[] source, T[] target)
            {
                int count = source?.Length ?? 0;
                if (count == 0) return Array.Empty<T>();
                if (target == null || target.Length != count) target = new T[count];
                Array.Copy(source, target, count);
                return target;
            }


            public bool TryRestoreStateSnapshot(
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot snapshot,
                CharacterTestRuntimeAiLabCompiledLogicProgram program)
            {
                if (program.Execution != null) return TryRestoreExecutionRuntimeSnapshot(snapshot.ExecutionRuntimeState);
                if (!snapshot.OrchestrationFailureDetails.IsValid || snapshot.CurrentStageIndex < -1 || snapshot.CurrentStageIndex >= program.Stages.Count
                    || snapshot.PrimaryActionStageIndex < -1
                    || snapshot.PrimaryActionStageIndex >= program.Stages.Count)
                    return false;
                _stage = snapshot.PrimaryActionStageIndex >= 0
                    ? program.Stages[snapshot.PrimaryActionStageIndex]
                    : null;
                _actionState = (CharacterTestRuntimeAiLabLogicActionState)snapshot.PrimaryActionState;
                _commandPending = snapshot.CommandPending;
                _commandRejected = snapshot.CommandRejected;
                _actionObserved = snapshot.ActionObserved;
                _observedActionInstanceId = snapshot.PrimaryActionInstanceId;
                _cancelRequested = snapshot.CancelRequested;
                _clearControlPending = snapshot.ClearControlPending;
                _enteredFrame = snapshot.ActionEnteredFrame;
                _orchestrationState =
                    (CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState)snapshot.OrchestrationState;
                _orchestrationOperation =
                    (CharacterTestBehaviorRecipeOrchestrationOperation)snapshot.OrchestrationOperation;
                _orchestrationTargetStage = snapshot.OrchestrationTargetTimelineIndex >= 0
                    && snapshot.OrchestrationTargetTimelineIndex < program.Stages.Count
                        ? program.Stages[snapshot.OrchestrationTargetTimelineIndex]
                        : null;
                _orchestrationResourceId = snapshot.OrchestrationResourceId;
                _orchestrationCommandPending = snapshot.OrchestrationCommandPending;
                _orchestrationCommandSubmitted = snapshot.OrchestrationCommandSubmitted;
                _orchestrationRejectAfterCleanup = snapshot.OrchestrationRejectAfterCleanup;
                _orchestrationPreviousInstanceId = snapshot.OrchestrationPreviousInstanceId;
                _orchestrationFailureReasonId = snapshot.OrchestrationFailureReasonId;
                _orchestrationFailureDetailId = snapshot.OrchestrationFailureDetailId;
                _orchestrationFailureDetails = snapshot.OrchestrationFailureDetails;
                _orchestrationFailureText = CharacterTestBehaviorFailureCodes.Display((CharacterTestBehaviorFailureCode)_orchestrationFailureReasonId);
                _orchestrationExpectedActionId = ResolveExpectedOrchestrationActionId(
                    _orchestrationTargetStage);
                _orchestrationPreviousRejectReason = _entity?.Action.LastRejection ?? default;
                return Enum.IsDefined(typeof(CharacterTestRuntimeAiLabLogicActionState), _actionState)
                    && Enum.IsDefined(
                        typeof(CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState),
                        _orchestrationState);
            }

            private CharacterActionId ResolveExpectedOrchestrationActionId(
                CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                if (stage == null)
                    return CharacterActionId.None;
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction)
                    return stage.CharacterActionId;
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility
                    && _abilityRuntime != null
                    && _abilityRuntime.Definitions.TryGet(
                        stage.AbilityId,
                        out AbilityDefinitionConfig ability))
                {
                    return ability.ActionBinding?.ActionId ?? CharacterActionId.None;
                }
                return CharacterActionId.None;
            }

            private static int FindStageIndex(
                CharacterTestRuntimeAiLabCompiledLogicProgram program,
                CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                for (int i = 0; program != null && stage != null && i < program.Stages.Count; i++)
                {
                    if (ReferenceEquals(program.Stages[i], stage))
                        return i;
                }
                return -1;
            }

            private Vector2 ResolveMove()
            {
                return TryResolveMovementTarget(_stage, out CharacterTestRuntimeAiLabMovementTargetSample target)
                    ? CharacterTestRuntimeAiLabMovementResolver.Resolve(_stage, target, _entityId)
                    : Vector2.zero;
            }

            private bool IsMovementStageEligible(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                if (stage == null || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
                    return true;
                return TryResolveMovementTarget(stage, out CharacterTestRuntimeAiLabMovementTargetSample target)
                    && CharacterTestRuntimeAiLabMovementResolver.IsEligible(stage, target);
            }

            private bool TryResolveMovementTarget(
                CharacterTestRuntimeAiLabCompiledLogicStage stage,
                out CharacterTestRuntimeAiLabMovementTargetSample sample)
            {
                sample = default;
                if (stage == null || _entity == null)
                    return false;

                switch (stage.MovementTargetKind)
                {
                    case CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget:
                        if (_facts == null || !_facts.TargetExists)
                            return false;
                        Vector2 currentToward = _facts.MoveDirection;
                        CharacterTestCharacterEntity currentTarget = FindEntity(_facts.TargetEntityId);
                        if (currentTarget != null)
                            currentToward = DirectionTo(currentTarget, out _);
                        sample = new CharacterTestRuntimeAiLabMovementTargetSample(
                            true,
                            _facts.TargetDistance,
                            currentToward,
                            _facts.MoveDirection);
                        return true;
                    case CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition:
                        Vector2 toPosition = new Vector2(
                            stage.MoveTargetPositionX - _entity.Transform.X,
                            stage.MoveTargetPositionZ - _entity.Transform.Z);
                        float positionDistance = toPosition.magnitude;
                        Vector2 positionDirection = positionDistance > 0.0001f
                            ? toPosition / positionDistance
                            : Vector2.zero;
                        sample = new CharacterTestRuntimeAiLabMovementTargetSample(
                            true,
                            positionDistance,
                            positionDirection,
                            positionDirection);
                        return true;
                    case CharacterTestRuntimeAiLabMovementTargetKind.Entity:
                        CharacterTestCharacterEntity entityTarget = stage.MoveTargetEntityId > 0
                            ? FindEntity(stage.MoveTargetEntityId)
                            : FindAutomaticAlly();
                        if (entityTarget == null)
                            return false;
                        Vector2 entityDirection = DirectionTo(entityTarget, out float entityDistance);
                        Vector2 entityNavigation = entityDirection;
                        if (_navigationService != null
                            && _navigationService.TryResolveMove(
                                _entity,
                                entityTarget,
                                stage.MoveMinDistance,
                                out CharacterTestAiNavigationResult navigation))
                        {
                            entityNavigation = navigation.Move;
                        }
                        sample = new CharacterTestRuntimeAiLabMovementTargetSample(
                            true,
                            entityDistance,
                            entityDirection,
                            entityNavigation);
                        return true;
                    default:
                        return false;
                }
            }

            private CharacterTestCharacterEntity FindEntity(int entityId)
            {
                for (int i = 0; _entities != null && i < _entities.Count; i++)
                {
                    CharacterTestCharacterEntity candidate = _entities[i];
                    if (candidate != null && candidate.Id.Value == entityId)
                        return candidate;
                }
                return null;
            }

            private CharacterTestCharacterEntity FindAutomaticAlly()
            {
                CharacterTestCharacterEntity selected = null;
                int selectedId = int.MaxValue;
                int teamId = _entity.Team.TeamId;
                for (int i = 0; _entities != null && i < _entities.Count; i++)
                {
                    CharacterTestCharacterEntity candidate = _entities[i];
                    if (candidate == null
                        || candidate.Id.Value == _entityId
                        || candidate.Team.TeamId != teamId
                        || !candidate.CombatState.IsAlive
                        || candidate.Id.Value >= selectedId)
                    {
                        continue;
                    }
                    selected = candidate;
                    selectedId = candidate.Id.Value;
                }
                return selected;
            }

            private Vector2 DirectionTo(
                CharacterTestCharacterEntity target,
                out float distance)
            {
                if (_perceptionFrameCache != null
                    && _perceptionFrameCache.TryGetTargetGeometry(
                        _frame,
                        _entity,
                        target,
                        out CharacterTestTargetGeometrySample geometry))
                {
                    distance = geometry.DistanceMeters;
                    return new Vector2(geometry.DirectionX, geometry.DirectionZ);
                }

                Vector2 toTarget = new Vector2(
                    target.Transform.X - _entity.Transform.X,
                    target.Transform.Z - _entity.Transform.Z);
                distance = toTarget.magnitude;
                return distance > 0.0001f ? toTarget / distance : Vector2.zero;
            }

            private static bool IsLegacyMovement(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                if (stage == null || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
                    return false;
                return Enum.IsDefined(
                    typeof(CharacterTestRuntimeAiLabMovementStrategy),
                    stage.MovementStrategy);
            }

        }
    }
}
