namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        /// <remarks>LowFreqAlloc. Called once at entry, after a completed simulation boundary.</remarks>
        internal int[] CaptureCurrentAttackWindowIds(int targetId, long actionInstanceId)
            => CharacterTestAttackWindowObservation.Capture(_windowState.ActiveTraces, targetId, actionInstanceId);

        /// <remarks>NoAlloc. Uses actual Enter/Exit ownership, including interruption and replacement.</remarks>
        internal bool AnyCapturedAttackWindowActive(int targetId, long actionInstanceId, int[] windows)
            => CharacterTestAttackWindowObservation.AnyCapturedActive(_windowState.ActiveTraces, targetId, actionInstanceId, windows);
    }
}
