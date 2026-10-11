using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTeamRelationProvider : ITeamRelationProvider
    {
        private readonly Dictionary<int, CharacterTestCharacterEntity> _entitiesByCombatId =
            new Dictionary<int, CharacterTestCharacterEntity>(128);
        private CharacterTestCharacterRuntimeWorld _world;
        private CharacterTestCharacterPhysicsRuntime _physicsRuntime;

        public void Configure(CharacterTestCharacterRuntimeWorld world, CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            _world = world;
            _physicsRuntime = physicsRuntime;
            _entitiesByCombatId.Clear();
            if (world == null || physicsRuntime == null)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null)
                    continue;

                int combatId = CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value;
                _entitiesByCombatId[combatId] = entity;
            }
        }

        public bool AreHostile(CombatEntityId a, CombatEntityId b)
        {
            CharacterTestCharacterEntity left = FindEntity(a);
            CharacterTestCharacterEntity right = FindEntity(b);
            if (left == null || right == null)
                return false;

            return left.Team.TeamId != right.Team.TeamId;
        }

        public bool AreFriendly(CombatEntityId a, CombatEntityId b)
        {
            return IsSameTeam(a, b);
        }

        public bool IsSameTeam(CombatEntityId a, CombatEntityId b)
        {
            CharacterTestCharacterEntity left = FindEntity(a);
            CharacterTestCharacterEntity right = FindEntity(b);
            if (left == null || right == null)
                return false;

            return left.Team.TeamId == right.Team.TeamId;
        }

        private CharacterTestCharacterEntity FindEntity(CombatEntityId combatEntityId)
        {
            if (_world == null || _physicsRuntime == null)
                return null;

            if (_entitiesByCombatId.TryGetValue(combatEntityId.Value, out CharacterTestCharacterEntity cached))
                return cached;

            int entityId = combatEntityId.Value - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
            if (entityId <= 0)
                return null;

            IReadOnlyList<CharacterTestCharacterEntity> entities = _world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id.Value == entityId)
                {
                    _entitiesByCombatId[combatEntityId.Value] = entities[i];
                    return entities[i];
                }
            }

            return null;
        }
    }
}
