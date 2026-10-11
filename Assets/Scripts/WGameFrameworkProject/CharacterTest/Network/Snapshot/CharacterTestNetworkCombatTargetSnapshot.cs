using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestNetworkCombatTargetSnapshot
    {
        public int SourceEntityId { get; set; }
        public int TargetEntityId { get; set; }
        public int TargetGameplayEntityIndex { get; set; }
        public int TargetGameplayEntityGeneration { get; set; }
        public int TargetCombatEntityId { get; set; }
        public int DamageZoneId { get; set; }
        public string DamageZoneLabel { get; set; }
        public int HitZoneId { get; set; }
        public string HitZoneLabel { get; set; }
        public int LogicalHurtboxId { get; set; }
        public string LogicalHurtboxLabel { get; set; }
        public int BindingRevisionAtSelection { get; set; }
        public long SelectedFrame { get; set; }

        public bool IsEmpty => SourceEntityId <= 0
            && TargetEntityId <= 0
            && TargetGameplayEntityIndex <= 0
            && TargetGameplayEntityGeneration <= 0
            && TargetCombatEntityId <= 0
            && DamageZoneId <= 0
            && HitZoneId <= 0
            && LogicalHurtboxId <= 0;

        internal static CharacterTestNetworkCombatTargetSnapshot FromHandle(
            in CharacterTestCombatTargetHandle handle)
        {
            if (!handle.HasTarget)
                return null;
            return new CharacterTestNetworkCombatTargetSnapshot
            {
                SourceEntityId = handle.SourceEntityId,
                TargetEntityId = handle.TargetEntityId,
                TargetGameplayEntityIndex = handle.TargetGameplayEntityId.Index,
                TargetGameplayEntityGeneration = handle.TargetGameplayEntityId.Generation,
                TargetCombatEntityId = handle.TargetCombatEntityId.Value,
                DamageZoneId = handle.DamageZoneId,
                DamageZoneLabel = handle.DamageZoneLabel,
                HitZoneId = handle.HitZoneId,
                HitZoneLabel = handle.HitZoneLabel,
                LogicalHurtboxId = handle.LogicalHurtboxId,
                LogicalHurtboxLabel = handle.LogicalHurtboxLabel,
                BindingRevisionAtSelection = handle.BindingRevisionAtSelection,
                SelectedFrame = handle.SelectedFrame.Value
            };
        }

        internal bool TryToHandle(out CharacterTestCombatTargetHandle handle)
        {
            handle = default;
            if (SourceEntityId <= 0
                || TargetEntityId <= 0
                || TargetGameplayEntityIndex <= 0
                || TargetGameplayEntityGeneration <= 0
                || TargetCombatEntityId <= 0
                || LogicalHurtboxId <= 0
                || SelectedFrame < 0L)
            {
                return false;
            }

            handle = new CharacterTestCombatTargetHandle(
                SourceEntityId,
                TargetEntityId,
                new GameplayEntityId(
                    TargetGameplayEntityIndex,
                    TargetGameplayEntityGeneration),
                new CombatEntityId(TargetCombatEntityId),
                DamageZoneId,
                HitZoneId,
                LogicalHurtboxId,
                BindingRevisionAtSelection,
                new RuntimeFrame(SelectedFrame),
                DamageZoneLabel,
                HitZoneLabel,
                LogicalHurtboxLabel);
            return handle.HasTarget;
        }

        internal CharacterTestNetworkCombatTargetSnapshot Clone()
        {
            return new CharacterTestNetworkCombatTargetSnapshot
            {
                SourceEntityId = SourceEntityId,
                TargetEntityId = TargetEntityId,
                TargetGameplayEntityIndex = TargetGameplayEntityIndex,
                TargetGameplayEntityGeneration = TargetGameplayEntityGeneration,
                TargetCombatEntityId = TargetCombatEntityId,
                DamageZoneId = DamageZoneId,
                DamageZoneLabel = DamageZoneLabel,
                HitZoneId = HitZoneId,
                HitZoneLabel = HitZoneLabel,
                LogicalHurtboxId = LogicalHurtboxId,
                LogicalHurtboxLabel = LogicalHurtboxLabel,
                BindingRevisionAtSelection = BindingRevisionAtSelection,
                SelectedFrame = SelectedFrame
            };
        }

        internal void AddHash(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt(NetworkCombatTargetSnapshotHashFields.SourceEntityId, SourceEntityId);
            accumulator.AddInt(NetworkCombatTargetSnapshotHashFields.TargetEntityId, TargetEntityId);
            accumulator.AddInt(NetworkCombatTargetSnapshotHashFields.TargetGameplayEntityIndex, TargetGameplayEntityIndex);
            accumulator.AddInt(NetworkCombatTargetSnapshotHashFields.TargetGameplayEntityGeneration, TargetGameplayEntityGeneration);
            accumulator.AddInt(NetworkCombatTargetSnapshotHashFields.TargetCombatEntityId, TargetCombatEntityId);
            accumulator.AddInt(NetworkCombatTargetSnapshotHashFields.DamageZoneId, DamageZoneId);
            accumulator.AddInt(NetworkCombatTargetSnapshotHashFields.HitZoneId, HitZoneId);
            accumulator.AddInt(NetworkCombatTargetSnapshotHashFields.LogicalHurtboxId, LogicalHurtboxId);
        }

    }

    internal static class NetworkCombatTargetSnapshotHashFields
    {
        public const int SourceEntityId = 1;
        public const int TargetEntityId = 2;
        public const int TargetGameplayEntityIndex = 3;
        public const int TargetGameplayEntityGeneration = 4;
        public const int TargetCombatEntityId = 5;
        public const int DamageZoneId = 6;
        public const int HitZoneId = 7;
        public const int LogicalHurtboxId = 8;
    }
}
