using System;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Gameplay;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeWorld
    {
        internal System.Collections.Generic.KeyValuePair<int, TacticalCard.IMomentumLedger>[] CapturePausedMomentumOwners()
            => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(_tacticalMotion, pair => pair.Key));

        internal int PausedNextRuntimeEntityId => _nextEntityId;

        // Only a fresh unpublished runtime may import a projection over restored Gameplay allocation.
        // It does not allocate Gameplay IDs or overwrite restored attributes with package defaults.
        internal CharacterTestCharacterEntity ImportPausedCharacter(CharacterTestNetworkCharacterSnapshot row,
            CharacterTestPausedActorMap map, CharacterTestCharacterDefinition definition)
        {
            SessionSnapshotCheck.Require(row != null && map != null && definition.CharacterId > 0 && row.EntityId == map.Runtime &&
                FindEntity(row.EntityId) == null && _gameplayComponentWorld.IsAlive(map.Gameplay), "projection-identity");
            var package = CharacterTestGeneratedPackageSource.LoadPackage(definition.GeneratedPackageRoot);
            var entity = new CharacterTestCharacterEntity(_characterComponentWorld,
                new CharacterTestCharacterEntityId(row.EntityId),
                new CharacterTestCharacterIdentityComponent(definition.CharacterId, definition.DisplayName, definition.PrefabKey,
                    definition.AnimationBinding, definition.GeneratedPackageRoot, ResolveCharacterStableId(definition)),
                new CharacterTestCharacterTeamComponent(row.TeamId, definition.DefaultTeamId), row.CreateTransformComponent(),
                new CharacterTestCharacterEquipmentComponent(true, ResolveDefaultEquipmentStateStableId(package)),
                new CharacterTestCharacterControlComponent(row.ControlOwner, row.RuntimeAiPlannerProfileSlot,
                    row.CreateRuntimeAiBehaviorSelection()), map.Gameplay, definition.PhysicsProfile);
            entity.ReinitializeBodyPartState(package?.Geometry);
            _entities.Add(entity);
            _combatGameplayMap.Register(ToCombatEntityId(entity), map.Gameplay);
            return entity;
        }

        internal void CompletePausedProjectionImport(int nextRuntimeEntityId)
        {
            SessionSnapshotCheck.Require(nextRuntimeEntityId > 0 && _entities.TrueForAll(e => e.Id.Value < nextRuntimeEntityId),
                "runtime-allocator");
            _nextEntityId = nextRuntimeEntityId;
        }
    }
}
