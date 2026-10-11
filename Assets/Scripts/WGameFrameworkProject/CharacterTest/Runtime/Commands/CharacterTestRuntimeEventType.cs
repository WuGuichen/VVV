namespace MxFramework.Game.CharacterTest
{
    /// <summary>Frozen runtime event discriminators. Values are protocol-stable; do not reorder or reassign.</summary>
    public enum CharacterTestRuntimeEventType
    {
        CommandAccepted = 0,
        CommandRejected = 1,
        CharacterSpawnQueued = 2,
        CharacterSpawnRejected = 3,
        CameraModeChanged = 4,
        TargetLockChanged = 5,
        TargetLockRejected = 6,
        ControlIntentApplied = 7,
        ControlIntentRejected = 8,
        CharacterReviveApplied = 9,
        CharacterReviveRejected = 10,
        CharacterDestroyApplied = 11,
        CharacterDestroyRejected = 12,
        RuntimeAiBehaviorSelectionApplied = 13,
        RuntimeAiBehaviorSelectionRejected = 14,
        SurfaceProbeApplied = 15,
        SurfaceProbeRejected = 16,
        GenesisMovableObjectProbeApplied = 17,
        GenesisMovableObjectProbeRejected = 18,
        GenesisPotencyMechanicsProbeApplied = 19,
        GenesisPotencyMechanicsProbeRejected = 20,
        GenesisLivingBeingProbeApplied = 21,
        GenesisLivingBeingProbeRejected = 22,
        GenesisLivingNaturalAttributesProbeApplied = 23,
        GenesisLivingNaturalAttributesProbeRejected = 24,
        GenesisLivingNaturalAttributesRuntimeStateApplied = 25,
        GenesisLivingNaturalAttributesRuntimeStateRejected = 26,
        MovableNaturalObjectResetApplied = 27,
        MovableNaturalObjectResetRejected = 28,
        MovableNaturalObjectPotencyInfluenceApplied = 29,
        MovableNaturalObjectPotencyInfluenceRejected = 30,
    }
}
