using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestMechanicSourceRuntime
    {
        private readonly int _accessoryProducerKind;
        private readonly CharacterTestAccessoryBuffResults _accessoryBuffResults = new CharacterTestAccessoryBuffResults();
        private void PublishAccessoryBuffOrigins(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            if (_accessoryBuffResults.Overflowed) throw new InvalidOperationException("accessory:MechanicBuffReceiptCapacityChanged");
            if (!world.GameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> store)) return;
            for (int i = 0; i < _accessoryBuffResults.Count; i++) {
                var receipt = _accessoryBuffResults.At(i);
                if (receipt.Status != GameplayBuffApplicationResultStatus.Succeeded
                    || receipt.Correlation.OperationKindId != _accessoryProducerKind) continue;
                long sourceId = receipt.Correlation.RequestId;
                var origin = world.DoubleEdgeState.FindProducer(_accessoryProducerKind, sourceId, 0);
                int combatSource = 0;
                GameplayEntityId sourceActor = default;
                foreach (var source in _sources) if (source.RuntimeSourceId == sourceId) { combatSource = source.SourceCombatEntityId.Value; sourceActor = source.SourceGameplayEntityId; break; }
                CharacterTestCharacterEntity attacker = null, target = null;
                foreach (var actor in world.Entities) {
                    if (actor.GameplayEntityId == sourceActor) attacker = actor;
                    if (actor.GameplayEntityId == receipt.TargetEntityId) target = actor;
                }
                bool enemy = attacker != null && target != null && attacker.Team.TeamId != target.Team.TeamId;
                if (!store.TryGet(receipt.TargetEntityId, out var buffs) || !buffs.TryGet(receipt.BuffId, out var buff)) continue;
                if (!world.DoubleEdgeState.BindBuffApplication(receipt.TargetEntityId, buff,
                    receipt.ReasonCode == GameplayBuffApplicationResultReasonCode.Refreshed
                        && (!_buffDefinitions.TryGet(receipt.BuffId, out var definition) || definition.StackPolicy != GameplayBuffStackPolicy.Replace), frame.Value,
                    combatSource, origin.Root, origin.Source, actualEnemy: enemy)) throw new InvalidOperationException("accessory:MechanicBuffBirthRejected");
            }
        }
    }
}
