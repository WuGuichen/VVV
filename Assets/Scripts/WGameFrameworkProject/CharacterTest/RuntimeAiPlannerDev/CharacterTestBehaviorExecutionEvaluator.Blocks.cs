using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestBehaviorExecutionEvaluator
    {
        private static readonly CharacterTestBehaviorExecutionPredicate LivingTarget = new(new CharacterTestRuntimeAiLabCardCondition {
            Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetState, Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
            AllowedTargetPhases = (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive });
        internal CharacterTestBehaviorExecutionEndReason EndReason => (CharacterTestBehaviorExecutionEndReason)_state.EndReason;

        private void ObserveBehindTarget(int index, in CharacterTestRuntimeAiLabMovementTargetSample sample)
        {
            if (_state.RearTargetIds[index] == sample.TargetEntityId) return;
            _state.RearTargetIds[index] = sample.TargetEntityId;
            _state.Stopped[index] = false;
            _state.RearOrbitSides[index] = 0;
        }

        private bool BlockHasBehindTarget(in CharacterTestBehaviorExecutionBlock block)
        {
            if (_state.RearOrbitSides.Length == 0) return false;
            foreach (var evt in _program.Events)
                if (evt.TimelineIndex == block.Timeline && evt.StartFrame >= block.Start && evt.EndFrame <= block.End
                    && evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                    && _program.Movements[evt.Parameter].BehindTarget.Enabled) return true;
            return false;
        }
        private bool UpdateExecutionBlocks(int frame)
        {
            if (_state.FinishRequested) return false;
            for (int i = 0; i < _program.Blocks.Length; i++)
            {
                var block = _program.Blocks[i];
                if (block.Timeline != _state.Timeline || _state.BlockResults[i] >= 2 || _state.LocalFrame < block.Start) continue;
                bool entering = _state.BlockStarts[i] < 0;
                if (entering)
                {
                    if (_state.LocalFrame >= block.End) continue;
                    _state.BlockStarts[i] = frame; _state.BlockResults[i] = 1;
                }
                if (block.Completion == CharacterTestBehaviorBlockCompletion.Delay || block.Completion == CharacterTestBehaviorBlockCompletion.Distance)
                {
                    if (entering) ReleaseControls(forceOwnedRelease: true);
                    long elapsed = (long)frame - _state.BlockStarts[i];
                    if (block.Completion == CharacterTestBehaviorBlockCompletion.Delay)
                    {
                        // Fixed duration is successful execution, not a failure watchdog.
                        if (elapsed >= block.Duration) { CompleteExecutionBlock(i, block); continue; }
                    }
                    else
                    {
                        if (!_runtime.Evaluate(LivingTarget)) return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TargetMissing);
                        // One simulation-frame entry boundary. Success wins at the exact deadline.
                        if (elapsed > 0 && Evaluate(_program.Events[block.CompletionEvent].Condition))
                        { CompleteExecutionBlock(i, block); continue; }
                        if (elapsed >= block.Duration) return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TimedOut);
                    }
                    _state.LocalFrame = _program.Events[block.CompletionEvent].StartFrame;
                    continue;
                }
                if (BlockHasBehindTarget(block))
                {
                    for (int e = 0; e < _program.Events.Length; e++)
                    {
                        var owned = _program.Events[e];
                        if (owned.TimelineIndex != block.Timeline || owned.StartFrame < block.Start || owned.EndFrame > block.End
                            || owned.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget) continue;
                        var movement = _program.Movements[owned.Parameter];
                        if (!movement.BehindTarget.Enabled) continue;
                        if (EvaluateExecutionPredicate(new CharacterTestBehaviorExecutionPredicate(movement, true), _runtime))
                            return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TargetMissing);
                        if (!_runtime.TrySampleMovement(movement, out var spatial)
                            || !CharacterTestBehindTargetResolver.TryRearError(spatial, out _))
                            return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.ConditionRejected);
                        ObserveBehindTarget(e, spatial);
                        if (movement.BehindTarget.Maintain && _state.LocalFrame >= block.End
                            && !CharacterTestBehindTargetResolver.Arrived(movement, spatial, _state.Stopped[e]))
                            return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TimedOut);
                    }
                }
                if (block.Completion == CharacterTestBehaviorBlockCompletion.Facing)
                {
                    if (!_runtime.Evaluate(LivingTarget))
                        return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TargetMissing);
                    // Observe the current target each tick. Alignment wins on the exact deadline.
                    if (Evaluate(_program.Events[block.CompletionEvent].Condition))
                    { CompleteExecutionBlock(i, block); continue; }
                    if ((long)frame - _state.BlockStarts[i] >= block.Duration)
                        return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TimedOut);
                    _state.LocalFrame = Math.Min(_state.LocalFrame, block.End - 1);
                    continue;
                }
                if (block.Completion == CharacterTestBehaviorBlockCompletion.Condition)
                {
                    if (!UpdateAttackWaitBlock(i, block, frame)) return false;
                    continue;
                }
                if (block.Completion == CharacterTestBehaviorBlockCompletion.Arrived)
                {
                    var move = _program.Movements[_program.Events[block.CompletionEvent].Parameter];
                    // Legacy target movement remains timed; explicit point execution completes on arrival.
                    if (!move.BehindTarget.Enabled && move.Target != CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition)
                    {
                        if (_state.LocalFrame >= block.End) _state.BlockResults[i] = 2;
                        continue;
                    }
                    if (EvaluateExecutionPredicate(new CharacterTestBehaviorExecutionPredicate(move, true), _runtime))
                        return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TargetMissing);
                    if (EvaluateExecutionPredicate(new CharacterTestBehaviorExecutionPredicate(move, false), _runtime)
                        && Evaluate(block.SuccessCondition))
                    { CompleteExecutionBlock(i, block); continue; }
                    if (_state.LocalFrame >= block.End)
                        return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TimedOut);
                }
                else if (block.Completion == CharacterTestBehaviorBlockCompletion.Action)
                {
                    int ev = block.CompletionEvent;
                    var result = (CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult)_state.Results[ev];
                    if (result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed)
                    { CompleteExecutionBlock(i, block); continue; }
                    if (result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.CommitFailed
                        || result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Failed
                        || result == CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Rejected)
                        return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.ActionRejected);
                    if (block.MaximumGuardFrames > 0 && _state.ActionOwner == ev && _state.ActionInstanceId > 0
                        && _runtime.ActualActionInstanceId == _state.ActionInstanceId)
                    {
                        if (_runtime is not ICharacterTestBehaviorExecutionActionClock guardClock)
                            return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.ConditionRejected);
                        // When the authored Blocking span already meets the limit, its Recovery owns the exit.
                        // A shorter explicit limit still uses the existing owned stop/ack boundary.
                        if ((long)block.MaximumGuardFrames < (long)block.GuardActiveEndFrame - block.GuardActiveStartFrame + 1
                            && (long)guardClock.ActualActionFrame - block.GuardActiveStartFrame >= block.MaximumGuardFrames)
                        {
                            // A planned guard end is distinct from watchdog timeout. Complete only through
                            // the existing owned stop/acknowledgement boundary; failed cleanup stays failed.
                            CompleteExecutionBlock(i, block);
                            BeginStop();
                            return false;
                        }
                    }
                    // Alignment is a bounded preparation goal. Only an opted-in block may leave its
                    // wait on timeout; reaching the action entry requests playback directly.
                    if (_state.Counts[ev] == 0 && block.ContinueToActionOnPreparationTimeout
                        && _state.LocalFrame < _program.Events[ev].StartFrame
                        && (long)frame - _state.BlockStarts[i] >= block.PreparationTimeoutFrames)
                    {
                        _state.LocalFrame = _program.Events[ev].StartFrame;
                        _state.LastSampledFrame = _state.LocalFrame - 1;
                        _state.WaitEvent = -1;
                    }
                    // The nominal cursor must not start the next block before the action's receipt/completion.
                    // Its independent world-frame budget continues to advance while this cursor is held.
                    _state.LocalFrame = Math.Min(_state.LocalFrame, block.End - 1);
                    // Completion follows the admitted action's own progress, including blocks
                    // without explicit preparation budgets. The host still owns stall protection.
                    if (_state.ActionOwner == ev && _state.ActionInstanceId > 0
                        && _runtime.ActualActionInstanceId == _state.ActionInstanceId
                        && _runtime is ICharacterTestBehaviorExecutionActionClock actionClock)
                    {
                        long actionLimit = block.CompletionTimeoutFrames > 0
                            ? block.CompletionTimeoutFrames : (long)block.Duration + CharacterTestBehaviorExecutionBlock.ActionBoundaryFrames;
                        if (actionClock.ActualActionFrame >= actionLimit)
                            return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TimedOut);
                        continue;
                    }
                    if (block.PreparationTimeoutFrames > 0)
                    {
                        long elapsed = (long)frame - (_state.Counts[ev] == 0 ? _state.BlockStarts[i] : _state.LastExecutions[ev]);
                        long limit = _state.Counts[ev] == 0
                            ? (long)block.PreparationTimeoutFrames + (_state.LocalFrame >= _program.Events[ev].StartFrame
                                ? CharacterTestBehaviorExecutionBlock.ActionBoundaryFrames : 0)
                            : block.CompletionTimeoutFrames;
                        if (elapsed >= limit) return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TimedOut);
                        continue;
                    }
                }
                else if (_state.LocalFrame >= block.End)
                { _state.BlockResults[i] = 2; continue; }
                if ((long)frame - _state.BlockStarts[i] >= block.Budget)
                    return FailExecutionBlock(i, CharacterTestBehaviorExecutionEndReason.TimedOut);
            }
            return true;
        }

        private bool IsCompletedGuardBlock(int eventIndex)
        {
            for (int i = 0; i < _program.Blocks.Length; i++)
                if (_state.BlockResults[i] == 2 && _program.Blocks[i].MaximumGuardFrames > 0
                    && _program.Blocks[i].CompletionEvent == eventIndex) return true;
            return false;
        }

        private bool IsCompletionAction(int eventIndex)
        {
            for (int i = 0; i < _program.Blocks.Length; i++)
                if (_state.BlockResults[i] == 1 && _program.Blocks[i].Completion == CharacterTestBehaviorBlockCompletion.Action
                    && _program.Blocks[i].CompletionEvent == eventIndex) return true;
            return false;
        }

        /// <remarks>NoAlloc. The caller validates the owned action's Finished or freely actionable Recovery state.</remarks>
        internal bool TryCompleteTerminalAction(long instanceId)
        {
            if (State != CharacterTestRuntimeAiLabLogicRuntimeState.Running || instanceId <= 0
                || _state.ActionInstanceId != instanceId || _state.ActionOwner < 0
                || _state.PendingAction >= 0 || _state.StopPending || _state.FinishRequested || _state.JumpTarget >= 0)
                return false;
            int end = _program.Timelines[_state.Timeline].DurationFrames;
            for (int i = 0; i < _program.Blocks.Length; i++)
            {
                var block = _program.Blocks[i];
                if (block.Timeline != _state.Timeline || block.Completion != CharacterTestBehaviorBlockCompletion.Action
                    || block.CompletionEvent != _state.ActionOwner || block.End != end) continue;
                // Only the terminal action block can end the card here; authored successors remain scheduled.
                CompleteExecutionBlock(i, block);
                // Relinquish behavior ownership without stopping the physical action's recovery.
                _state.ActionOwner = -1;
                _state.ActionInstanceId = 0;
                _runtime.ObserveOwnedAction(0L);
                Stop();
                return true;
            }
            return false;
        }

        private void CompleteExecutionBlock(int index, in CharacterTestBehaviorExecutionBlock block)
        {
            _state.BlockResults[index] = 2;
            if (_state.AttackWaitBlock == index) ClearAttackWait();
            ReleaseControls();
            if (block.CompletionEvent >= 0) SetResult(block.CompletionEvent, CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed);
            _state.LocalFrame = Math.Max(_state.LocalFrame, block.End);
            _state.LastSampledFrame = _state.LocalFrame - 1;
            _state.WaitEvent = -1;
        }

        private bool FailExecutionBlock(int index, CharacterTestBehaviorExecutionEndReason reason)
        {
            if (index >= 0) _state.BlockResults[index] = 3;
            if (_state.AttackWaitBlock == index) ClearAttackWait();
            _state.EndReason = (int)reason;
            Stop(true);
            return false;
        }

        private bool CleanupBudgetExpired(int frame)
        {
            if (_state.StopStartedFrame < 0 || (!_state.FinishRequested && _state.JumpTarget < 0
                    && !(_state.StopPending && IsCompletedGuardBlock(_state.ActionOwner)))
                || (long)frame - _state.StopStartedFrame < CharacterTestBehaviorExecutionBlock.CleanupFrames) return false;
            ReleaseControls();
            _state.EndReason = (int)CharacterTestBehaviorExecutionEndReason.CleanupTimedOut;
            _state.FinishRequested = true; _state.FailureRequested = true;
            _state.State = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Failed;
            return true;
        }
    }
}
