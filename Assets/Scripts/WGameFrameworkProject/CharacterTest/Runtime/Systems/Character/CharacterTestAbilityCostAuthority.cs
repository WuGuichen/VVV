using System;
using System.Collections.Generic;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Commits action-only ability costs against formal character attributes.</summary>
    internal sealed partial class CharacterTestAbilityCostAuthority : IAbilityCostAuthority
    {
        private readonly CharacterTestCharacterRuntimeWorld _world;

        public CharacterTestAbilityCostAuthority(CharacterTestCharacterRuntimeWorld world)
        {
            _world = world;
        }

        public bool CanAfford(
            int casterEntityId,
            IReadOnlyList<AbilityCostConfig> costs,
            out int missingAttributeId)
        {
            missingAttributeId = 0;
            CharacterTestCharacterEntity entity = FindEntity(casterEntityId);
            if (entity == null)
                return false;

            if (_fixedScope != null) return _fixedScope.CanAfford(entity, costs, out missingAttributeId);
            _world.TryGetTacticalMotion(casterEntityId,out var motion);
            return CharacterTestAbilityCosts.CanAfford(_world.GameplayComponentWorld,entity.GameplayEntityId,motion,costs,0,out missingAttributeId);
        }

        public bool TryCommitCosts(int casterEntityId,IReadOnlyList<AbilityCostConfig> costs,out int missingAttributeId)
        {
            missingAttributeId=0;var entity=FindEntity(casterEntityId);if(entity==null)return false;
            if (_fixedScope != null) return _fixedScope.TryCommit(entity, costs, out missingAttributeId);
            _world.TryGetTacticalMotion(casterEntityId,out var motion);
            return CharacterTestAbilityCosts.TryCommit(_world.GameplayComponentWorld,entity.GameplayEntityId,motion,costs,out missingAttributeId);
        }

        private CharacterTestCharacterEntity FindEntity(int entityId)
        {
            if (_world == null || entityId <= 0)
                return null;
            IReadOnlyList<CharacterTestCharacterEntity> entities = _world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] != null && entities[i].Id.Value == entityId)
                    return entities[i];
            }
            return null;
        }
    }
}
