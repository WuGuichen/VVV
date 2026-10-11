using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestBehaviorExecutionEvaluator
    {
        private static int SnapshotVersion(CharacterTestBehaviorExecutionProgram program)
        {
            foreach (var movement in program.Movements)
                if (movement.BehindTarget.Enabled) return CharacterTestBehaviorExecutionState.BehindTargetVersion;
            foreach (var block in program.Blocks)
                if (block.Completion == CharacterTestBehaviorBlockCompletion.Condition)
                    return CharacterTestBehaviorExecutionState.CurrentVersion;
            return CharacterTestBehaviorExecutionState.LegacyVersion;
        }
        private void ClearAttackWait()
        {
            _state.AttackWaitBlock = -1; _state.AttackWaitTargetId = 0;
            _state.AttackWaitInstanceId = 0; _state.AttackWaitWindowIds = Array.Empty<int>();
        }

        /// <remarks>LowFreqAlloc only at entry. Subsequent window-set checks are NoAlloc.</remarks>
        private bool UpdateAttackWaitBlock(int index, in CharacterTestBehaviorExecutionBlock block, int frame)
        {
            if (_runtime is not ICharacterTestBehaviorExecutionAttackWindowRuntime windows)
                return FailExecutionBlock(index, CharacterTestBehaviorExecutionEndReason.ConditionRejected);
            bool entering = _state.AttackWaitBlock != index;
            if (entering)
            {
                if (!windows.TryCaptureTargetAttackWindows(out int target, out long instance, out int[] ids)
                    || target <= 0 || instance < 0 || ids == null)
                    return FailExecutionBlock(index, !_runtime.Evaluate(LivingTarget)
                        ? CharacterTestBehaviorExecutionEndReason.TargetMissing : CharacterTestBehaviorExecutionEndReason.ConditionRejected);
                _state.AttackWaitBlock = index; _state.AttackWaitTargetId = target;
                _state.AttackWaitInstanceId = instance; _state.AttackWaitWindowIds = ids;
            }
            if (!windows.IsCapturedTargetAvailable(_state.AttackWaitTargetId))
                return FailExecutionBlock(index, CharacterTestBehaviorExecutionEndReason.TargetMissing);
            if (entering && block.PassiveWait) ReleaseControls(forceOwnedRelease: true);
            // Keep the existing one-world-frame entry boundary for both revisions.
            // Let the legacy entry sample submit its zero movement claim before any same-tick cleanup
            // can replace it with an owner-qualified release. This is a command boundary, not
            // a fabricated attack window; an empty captured set never waits for a future hit.
            if (frame == _state.BlockStarts[index]) return true;
            if (!windows.AnyCapturedTargetAttackWindowActive(_state.AttackWaitTargetId, _state.AttackWaitInstanceId, _state.AttackWaitWindowIds)
                && Evaluate(_program.Events[block.CompletionEvent].Condition) && Evaluate(block.SuccessCondition))
            { CompleteExecutionBlock(index, block); return true; }
            if ((long)frame - _state.BlockStarts[index] >= block.Duration)
                return FailExecutionBlock(index, CharacterTestBehaviorExecutionEndReason.TimedOut);
            _state.LocalFrame = _program.Events[block.CompletionEvent].StartFrame;
            return true;
        }

        private static bool CompareAttackWait(bool ended, in CharacterTestBehaviorExecutionPredicate predicate)
            => predicate.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals ? ended == predicate.BoolValue
                : predicate.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals && ended != predicate.BoolValue;

        private bool ValidAttackWaitSnapshot(CharacterTestBehaviorExecutionState source)
        {
            if (!ValidIndex(source.AttackWaitBlock, _program.Blocks.Length) || source.AttackWaitWindowIds == null
                || source.AttackWaitInstanceId < 0) return false;
            if (source.AttackWaitBlock < 0)
                return source.AttackWaitTargetId == 0 && source.AttackWaitInstanceId == 0 && source.AttackWaitWindowIds.Length == 0;
            var block = _program.Blocks[source.AttackWaitBlock];
            if (block.Completion != CharacterTestBehaviorBlockCompletion.Condition || block.Timeline != source.Timeline
                || source.BlockResults[source.AttackWaitBlock] != 1 || source.AttackWaitTargetId <= 0
                || source.AttackWaitWindowIds.Length > 0 && source.AttackWaitInstanceId == 0) return false;
            for (int i = 0; i < source.AttackWaitWindowIds.Length; i++)
            {
                if (source.AttackWaitWindowIds[i] <= 0) return false;
                for (int j = 0; j < i; j++) if (source.AttackWaitWindowIds[i] == source.AttackWaitWindowIds[j]) return false;
            }
            return true;
        }
    }
}
