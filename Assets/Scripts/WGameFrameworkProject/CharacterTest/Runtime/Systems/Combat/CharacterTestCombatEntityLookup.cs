using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestCombatEntityLookup : IHitTargetStateCandidateResolver
    {
        private readonly Dictionary<int, CharacterTestCharacterEntity> _entitiesById =
            new Dictionary<int, CharacterTestCharacterEntity>(128);
        private CharacterTestCharacterRuntimeWorld _world;
        internal TacticalCard.TacticalDoubleEdgeBattleState AccessoryState => _world?.DoubleEdgeState;
        internal CharacterTestCharacterRuntimeWorld RuntimeWorld => _world;
        private HitTargetStateContributionStore _targetStateContributions;

        public void Configure(
            CharacterTestCharacterRuntimeWorld world,
            HitTargetStateContributionStore targetStateContributions = null)
        {
            _world = world;
            _targetStateContributions = targetStateContributions;
            _entitiesById.Clear();
            if (world == null)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null)
                    _entitiesById[entity.Id.Value] = entity;
            }
        }

        public CharacterTestCharacterEntity FindEntity(int entityId)
        {
            if (_world == null || entityId <= 0)
                return null;

            if (_entitiesById.TryGetValue(entityId, out CharacterTestCharacterEntity cached))
                return cached;

            IReadOnlyList<CharacterTestCharacterEntity> entities = _world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id.Value == entityId)
                {
                    _entitiesById[entityId] = entities[i];
                    return entities[i];
                }
            }

            return null;
        }

        public CharacterTestCharacterEntity FindEntityByCombatId(CombatEntityId combatEntityId)
        {
            int entityId = ResolveCharacterEntityId(combatEntityId);
            return entityId <= 0 ? null : FindEntity(entityId);
        }

        public int ReadAttribute(CombatEntityId combatEntityId, int attributeId)
        {
            CharacterTestCharacterEntity entity = FindEntityByCombatId(combatEntityId);
            return _world != null ? _world.GetCurrentAttribute(entity, attributeId, includeModifiers: true) : 0;
        }

        public HitTargetStateFlags ResolveTargetState(CombatEntityId targetEntityId)
        {
            if (CharacterTestCombatHitEventResolver.IsStaticEnvironmentTarget(targetEntityId))
                return HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking;

            CharacterTestCharacterEntity entity = FindEntityByCombatId(targetEntityId);
            HitTargetStateFlags state = entity == null
                || _world == null
                || !entity.BattleState.CanBeTargeted
                || !entity.BattleState.CanReceiveHpDamage
                || _world.GetCurrentAttribute(entity, CharacterTestCharacterAttributeIds.Hp) <= 0
                ? HitTargetStateFlags.None
                : HitTargetStateFlags.Alive;
            if (state != HitTargetStateFlags.None && _targetStateContributions != null)
                state |= _targetStateContributions.ResolveFlags(targetEntityId);
            if ((state & HitTargetStateFlags.Alive) == 0)
                return HitTargetStateFlags.None;
            if (entity != null && entity.CombatState.IsCohesionBroken)
                state &= ~HitTargetStateFlags.Blocking;
            return state;
        }

        public HitTargetStateFlags ResolveTargetState(HitCandidate candidate)
        {
            var state = ResolveTargetState(candidate.TargetId);
            // Terrain's Blocking is a physical stop, not an actor's guard pose.
            if ((state & HitTargetStateFlags.Blocking) == 0
                || CharacterTestCombatHitEventResolver.IsStaticEnvironmentTarget(candidate.TargetId))
                return state;
            var entity = FindEntityByCombatId(candidate.TargetId);
            return entity != null && IsWithinOrdinaryGuardArc(entity.Transform.YawMilliDegrees, candidate.IncomingDirection)
                ? state : state & ~HitTargetStateFlags.Blocking;
        }

        /// <summary>Front horizontal half-space, including both 90-degree boundaries.</summary>
        /// <remarks>Zero/vertical-only motion has no observed horizontal incoming direction.
        /// It bypasses ordinary guard; no attacker-position or normal fallback is used.</remarks>
        internal static bool IsWithinOrdinaryGuardArc(int targetYawMilliDegrees, FixVector3 incomingDirection)
        {
            if (incomingDirection.X == Fix64.Zero && incomingDirection.Z == Fix64.Zero) return false;
            var pose = new CharacterTestDirectHitTraceSourcePose(0, FixVector3.Zero, targetYawMilliDegrees);
            var forward = pose.RotateLocal(new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));
            return forward.X * incomingDirection.X + forward.Z * incomingDirection.Z <= Fix64.Zero;
        }

        public static int ResolveCharacterEntityId(CombatEntityId combatEntityId)
        {
            if (combatEntityId.Value <= CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                return 0;

            return combatEntityId.Value - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
        }

        public static void TickCombatStates(CharacterTestCharacterRuntimeWorld world)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
                entities[i].TickCombatState();
        }
    }
}
