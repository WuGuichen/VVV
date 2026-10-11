using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Lab-only transaction probe. It grants no evasion, relocation, scaling or combat modifier.</summary>
    public sealed class TestPreparationPayload : TacticalUtilityPayload
    {
        public override UtilityPayloadKind Kind => UtilityPayloadKind.TestPreparation;
        internal static bool Matches(TacticalCardDefinition definition) =>
            definition is TacticalUtilityCardDefinition utility && utility.Payload is TestPreparationPayload;
    }

    /// <summary>Immutable observation of the preparation owned by one StageRun and actor generation.</summary>
    internal readonly struct TacticalTestPreparationState
    {
        internal readonly ulong StageRunId, CardInstanceId, ActivationRevision;
        internal readonly uint SourceCardVersion;
        internal readonly GameplayEntityId Actor;
        internal readonly long CompletedWorldFrame;
        internal readonly int PaidMomentum;
        internal bool IsActive => StageRunId != 0 && Actor.IsValid;

        internal TacticalTestPreparationState(ulong stageRunId, GameplayEntityId actor,
            ulong cardInstanceId, uint sourceCardVersion, ulong activationRevision, long completedWorldFrame, int paidMomentum)
        {
            StageRunId = stageRunId;
            Actor = actor;
            CardInstanceId = cardInstanceId;
            SourceCardVersion = sourceCardVersion;
            ActivationRevision = activationRevision;
            CompletedWorldFrame = completedWorldFrame;
            PaidMomentum = paidMomentum;
        }
    }
}
