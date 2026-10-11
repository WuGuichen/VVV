using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay
    {
        internal sealed partial class RuntimeBridge : ICharacterTestBehaviorExecutionAttackWindowRuntime
        {
            /// <remarks>LowFreqAlloc. No animation name, future plan interval or speculative action is sampled.</remarks>
            public bool TryCaptureTargetAttackWindows(out int targetId, out long actionInstanceId, out int[] windows)
            {
                targetId = 0; actionInstanceId = 0; windows = Array.Empty<int>();
                var target = CurrentExecutionTarget();
                if (target == null || !target.CombatState.IsAlive || _executionCharacterRuntime == null
                    || !_executionCharacterRuntime.TryGetLastCompletedRuntimeFrame(out _)) return false;
                targetId = target.Id.Value;
                var instance = _executionCharacterRuntime.ActionRuntime.GetActionInstanceForObservation(target.Id);
                if (instance == null || !instance.IsRunning) return true;
                actionInstanceId = instance.InstanceId;
                windows = _executionCharacterRuntime.CombatTraceSystem.CaptureCurrentAttackWindowIds(targetId, actionInstanceId);
                return true;
            }

            /// <remarks>NoAlloc. Target replacement is target loss for this captured wait.</remarks>
            public bool IsCapturedTargetAvailable(int targetId)
            {
                var target = CurrentExecutionTarget();
                return target != null && target.Id.Value == targetId && target.CombatState.IsAlive;
            }

            /// <remarks>NoAlloc. Validity is checked separately by the wait owner.</remarks>
            public bool AnyCapturedTargetAttackWindowActive(int targetId, long actionInstanceId, int[] windows)
                => _executionCharacterRuntime.CombatTraceSystem.AnyCapturedAttackWindowActive(targetId, actionInstanceId, windows);
        }
    }
}
