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
            public void BeginLogicCandidate(CharacterTestRuntimeAiLabLogicCandidateKind kind, int index, int stableRank)
            {
                _logicFacts.SetTraceLayer(CharacterTestRuntimeAiLabDecisionTraceLayer.LogicCondition);
                if (_logicCandidateCount >= _logicCandidates.Length)
                {
                    RecordLogicCandidateOverflow(CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.LogicConditionCandidates);
                    _activeLogicCandidateIndex = -1;
                    return;
                }
                RuntimeTraceIdentity identity = CandidateIdentity(kind, index);
                string label = kind == CharacterTestRuntimeAiLabLogicCandidateKind.Detector
                    ? _detectorCandidateStableIds[index] : _transitionCandidateStableIds[index];
                _activeLogicCandidateIndex = _logicCandidateCount;
                _logicCandidates[_logicCandidateCount++] = new CharacterTestRuntimeAiLabCandidateEvaluationTrace
                {
                    Identity = identity,
                    StableId = label,
                    Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.LogicCondition,
                    State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.NotEvaluated,
                    StableRank = stableRank,
                    ReasonCode = CharacterTestRuntimeAiLabDecisionTraceReasonCodes.NotEvaluated
                };
            }

            public void EndLogicCandidate(
                CharacterTestRuntimeAiLabDecisionTraceCandidateState state,
                int priority,
                int reasonCode)
            {
                if (_activeLogicCandidateIndex < 0 || _activeLogicCandidateIndex >= _logicCandidateCount)
                    return;
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate =
                    _logicCandidates[_activeLogicCandidateIndex];
                candidate.State = state;
                candidate.Priority = priority;
                candidate.PriorityAvailable = true;
                candidate.ReasonCode = reasonCode;
                _logicCandidates[_activeLogicCandidateIndex] = candidate;
                _activeLogicCandidateIndex = -1;
            }

            public void MarkLogicCandidateSelected(CharacterTestRuntimeAiLabLogicCandidateKind kind, int index, int reasonCode)
            {
                RuntimeTraceIdentity identity = CandidateIdentity(kind, index);
                for (int i = 0; i < _logicCandidateCount; i++)
                {
                    if (_logicCandidates[i].Identity != identity) continue;
                    CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = _logicCandidates[i];
                    candidate.State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected;
                    candidate.ReasonCode = reasonCode;
                    _logicCandidates[i] = candidate;
                    return;
                }
            }

            public void RecordStageActionCandidate(int stageIndex, bool selected, int reasonCode)
            {
                if (stageIndex < 0 || stageIndex >= _stageHasMainAction.Length || !_stageHasMainAction[stageIndex])
                    return;
                string candidateStableId = _stageCandidateStableIds[stageIndex];
                RuntimeTraceIdentity identity = CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.MainActionKind, stageIndex, _bindingNumericId);
                for (int i = 0; i < _logicCandidateCount; i++)
                {
                    if (_logicCandidates[i].Identity == identity)
                        return;
                    if (selected
                        && _logicCandidates[i].Layer == CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction
                        && _logicCandidates[i].State == CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected)
                    {
                        CharacterTestRuntimeAiLabCandidateEvaluationTrace previous = _logicCandidates[i];
                        previous.State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Suppressed;
                        previous.ReasonCode = CharacterTestAiTraceReason.LOGIC_STAGE_REPLACED;
                        _logicCandidates[i] = previous;
                    }
                }
                if (_logicCandidateCount >= _logicCandidates.Length)
                {
                    RecordLogicCandidateOverflow(
                        CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.MainActionCandidates);
                    return;
                }
                _logicCandidates[_logicCandidateCount++] = new CharacterTestRuntimeAiLabCandidateEvaluationTrace
                {
                    Identity = identity,
                    StableId = candidateStableId,
                    Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction,
                    State = selected
                        ? CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected
                        : CharacterTestRuntimeAiLabDecisionTraceCandidateState.Evaluated,
                    StableRank = stageIndex,
                    ReasonCode = reasonCode
                };
            }

            private void RecordLogicCandidateOverflow(
                CharacterTestRuntimeAiLabDecisionTraceOverflowRegion region)
            {
                _logicCandidateOverflowed = true;
                if (_logicCandidateOverflowRegion == CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.None)
                    _logicCandidateOverflowRegion = region;
                _logicCandidateOverflowActualCount = _logicCandidateCount + 1;
            }

            private RuntimeTraceIdentity CandidateIdentity(CharacterTestRuntimeAiLabLogicCandidateKind kind, int index)
            {
                switch (kind)
                {
                    case CharacterTestRuntimeAiLabLogicCandidateKind.Detector:
                        if ((uint)index >= (uint)_detectorCandidateStableIds.Length) throw new ArgumentOutOfRangeException(nameof(index));
                        return CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.DetectorKind, index, _bindingNumericId);
                    case CharacterTestRuntimeAiLabLogicCandidateKind.Transition:
                        if ((uint)index >= (uint)_transitionCandidateStableIds.Length) throw new ArgumentOutOfRangeException(nameof(index));
                        return CharacterTestAiEvidenceIds.Index(CharacterTestAiEvidenceIds.TransitionKind, index, _bindingNumericId);
                    default: throw new ArgumentOutOfRangeException(nameof(kind));
                }
            }

            public bool TryEnterStage(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                if (stage == null)
                    return false;
                for (int i = 0; i < stage.EntryConditions.Count; i++)
                {
                    if (!Evaluate(stage.EntryConditions[i]))
                        return false;
                }

                if (_orchestrationState ==
                        CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded
                    && ReferenceEquals(_orchestrationTargetStage, stage))
                {
                    bool previousOrchestrationStageOwnedMovement = _stage != null
                        && (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                            || IsLegacyMovement(_stage));
                    bool orchestrationLeavesMovementStage = previousOrchestrationStageOwnedMovement
                        && !ReferenceEquals(_stage, stage);
                    _stage = stage;
                    _enteredFrame = ClampFrame(_frame.Value);
                    _commandPending = false;
                    _commandRejected = false;
                    _cancelRequested = false;
                    _clearControlPending = orchestrationLeavesMovementStage
                        || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait;
                    _frameScopedControlCommandSubmitted = false;
                    bool ownsRuntimeAction = stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                        || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility;
                    _actionObserved = ownsRuntimeAction;
                    _observedActionInstanceId = 0L;
                    if (ownsRuntimeAction)
                    {
                        if (_actionRuntime != null
                            && _actionRuntime.TryGetActiveActionPresentationState(
                                new CharacterTestCharacterEntityId(_entityId),
                                out long observedActionInstanceId,
                                out _,
                                out _,
                                out _))
                        {
                            _observedActionInstanceId = observedActionInstanceId;
                        }
                    }
                    _actionStartedThisFrame = ownsRuntimeAction;
                    _actionState = stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic
                        ? CharacterTestRuntimeAiLabLogicActionState.None
                        : CharacterTestRuntimeAiLabLogicActionState.Running;
                    return true;
                }

                if (_adoptEntryStagePending && ReferenceEquals(_stage, stage))
                {
                    _adoptEntryStagePending = false;
                    _enteredFrame = ClampFrame(_frame.Value);
                    _commandPending = false;
                    _commandRejected = false;
                    _actionObserved = true;
                    _actionStartedThisFrame = true;
                    _cancelRequested = false;
                    _clearControlPending = false;
                    _frameScopedControlCommandSubmitted = false;
                    _actionState = CharacterTestRuntimeAiLabLogicActionState.Running;
                    return true;
                }

                // PreserveAction moves only the evaluator's logical cursor. A non-owning target
                // must not replace the action stage that still owns lifecycle observation and
                // cancellation while its CharacterAction/control intent is running.
                if (!stage.OwnsPrimaryAction
                    && _stage != null
                    && _stage.OwnsPrimaryAction
                    && _actionState == CharacterTestRuntimeAiLabLogicActionState.Running)
                {
                    return true;
                }

                bool previousStageOwnedMovement = _stage != null
                    && (_stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        || IsLegacyMovement(_stage));
                bool leavesMovementStage = previousStageOwnedMovement
                    && !ReferenceEquals(_stage, stage);
                _stage = stage;
                _enteredFrame = ClampFrame(_frame.Value);
                _commandRejected = false;
                _actionObserved = false;
                _cancelRequested = false;
                _commandPending = stage.OwnsPrimaryAction;
                _clearControlPending = leavesMovementStage
                    || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait;
                _frameScopedControlCommandSubmitted = false;
                _actionState = stage.OwnsPrimaryAction || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait
                    ? CharacterTestRuntimeAiLabLogicActionState.Running
                    : CharacterTestRuntimeAiLabLogicActionState.None;
                return true;
            }

        }
    }
}
