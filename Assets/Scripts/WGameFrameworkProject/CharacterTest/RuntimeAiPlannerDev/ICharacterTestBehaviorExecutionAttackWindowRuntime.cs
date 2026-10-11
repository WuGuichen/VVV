namespace MxFramework.Game.CharacterTest
{
    /// <summary>Optional observation port. Unsupported observation must not masquerade as attack completion.</summary>
    internal interface ICharacterTestBehaviorExecutionAttackWindowRuntime
    {
        bool TryCaptureTargetAttackWindows(out int targetId, out long actionInstanceId, out int[] windows);
        bool IsCapturedTargetAvailable(int targetId);
        bool AnyCapturedTargetAttackWindowActive(int targetId, long actionInstanceId, int[] windows);
    }
}
