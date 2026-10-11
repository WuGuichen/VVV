using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Battle-local identity; only the owning deck changes card location.</summary>
    public sealed partial class TacticalCardInstance
    {
        public ulong InstanceId { get; }
        public TacticalCardDefinition Definition { get; }
        public CardZone Zone { get; private set; }
        public int UpgradeLevel { get; private set; }
        public uint Version { get; private set; }

        public TacticalCardInstance(ulong instanceId, TacticalCardDefinition definition, int upgradeLevel = 0)
        {
            if (instanceId == 0) throw new ArgumentOutOfRangeException(nameof(instanceId));
            if (upgradeLevel < 0) throw new ArgumentOutOfRangeException(nameof(upgradeLevel));
            InstanceId = instanceId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            UpgradeLevel = upgradeLevel;
            Zone = CardZone.DrawPile;
        }

        internal void MoveTo(CardZone zone)
        {
            if (Zone == zone) return;
            Zone = zone;
            Version = unchecked(Version + 1);
        }
    }

    /// <summary>Uncached, allocation-free legality result. Submission always re-evaluates.</summary>
    public readonly struct CardPlayEvaluation
    {
        public bool IsLegal { get; }
        public CardPlayFailure Failure { get; }
        public int EffectiveCost { get; }
        public int SpendableMomentum { get; }
        public int BlockingActionId { get; }
        public long RemainingCooldownFrames { get; }
        public int SimulationTickRate { get; }

        public CardPlayEvaluation(CardPlayFailure failure, int effectiveCost, int spendableMomentum,
            int blockingActionId = 0, long remainingCooldownFrames = 0, int simulationTickRate = 0)
        {
            IsLegal = failure == CardPlayFailure.None;
            Failure = failure;
            EffectiveCost = effectiveCost;
            SpendableMomentum = spendableMomentum;
            BlockingActionId = blockingActionId;
            RemainingCooldownFrames = remainingCooldownFrames;
            SimulationTickRate = simulationTickRate;
        }
    }
}
