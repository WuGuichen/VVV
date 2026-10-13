using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestGuardAuraRuntime
    {
        private readonly CharacterTestAccessoryBuffResults _accessoryBuffResults = new CharacterTestAccessoryBuffResults();
        private readonly List<OnHitBuffOrigin> _accessoryBuffOrigins = new List<OnHitBuffOrigin>();
        private Func<HitResolveResult, CharacterTestAccessoryHitEvidence> _buffEvidence;
        private Func<HitResolveResult, TacticalSecondBatchHit> _buffParentReceipt;
        private Func<HitResolveResult, int, GameplayBuffRequestCorrelation> _buffCorrelation;
        private readonly struct OnHitBuffOrigin
        {
            internal readonly HitResolveResult Hit;
            internal readonly CharacterTestAccessoryHitEvidence Evidence;
            internal readonly TacticalSecondBatchHit Receipt;
            internal OnHitBuffOrigin(HitResolveResult hit, CharacterTestAccessoryHitEvidence evidence, TacticalSecondBatchHit receipt)
            { Hit = hit; Evidence = evidence; Receipt = receipt; }
        }
        private GameplayBuffRequestCorrelation RecordBuffOrigin(HitResolveResult hit, int buffId)
        {
            int index = _accessoryBuffOrigins.Count;
            _accessoryBuffOrigins.Add(new OnHitBuffOrigin(hit, _buffEvidence != null ? _buffEvidence(hit) : default,
                _buffParentReceipt != null ? _buffParentReceipt(hit) : default));
            return new GameplayBuffRequestCorrelation((long)hit.Frame.Value + 1, index, 1);
        }
        private void PrepareAccessoryBuffOrigins()
        {
            _accessoryBuffOrigins.Clear();
            int maximum = 0;
            foreach (var hit in _currentOnHitResults) maximum = checked(maximum + (ResolveCurrentOnHitBuffs(hit)?.Length ?? 0));
            if (_accessoryBuffOrigins.Capacity < maximum) _accessoryBuffOrigins.Capacity = maximum;
        }
        private void PublishAccessoryBuffOrigins(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            if (_accessoryBuffResults.Overflowed) throw new InvalidOperationException("accessory:BuffReceiptCapacityChanged");
            if (!world.GameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> store)) return;
            for (int i = 0; i < _accessoryBuffResults.Count; i++) {
                var receipt = _accessoryBuffResults.At(i);
                if (receipt.Status != GameplayBuffApplicationResultStatus.Succeeded
                    || receipt.Correlation.RequestId != frame.Value + 1 || receipt.Correlation.OperationKindId != 1) continue;
                int index = receipt.Correlation.OperationIndex;
                if (index < 0 || index >= _accessoryBuffOrigins.Count) throw new InvalidOperationException("accessory:UnknownBuffReceipt");
                if (!store.TryGet(receipt.TargetEntityId, out var buffs) || !buffs.TryGet(receipt.BuffId, out var buff)) continue;
                var origin = _accessoryBuffOrigins[index];
                if (!world.DoubleEdgeState.BindBuffApplication(receipt.TargetEntityId, buff,
                    receipt.ReasonCode == GameplayBuffApplicationResultReasonCode.Refreshed
                        && (!_buffDefinitions.TryGet(receipt.BuffId, out var definition) || definition.StackPolicy != GameplayBuffStackPolicy.Replace), frame.Value,
                    origin.Hit.AttackerId.Value, origin.Evidence.DoubleEdgeSource, origin.Evidence.Lineage, origin.Receipt.Key, origin.Evidence.ActualEnemy))
                    throw new InvalidOperationException("accessory:BuffBirthRejected");
            }
        }
    }
}
