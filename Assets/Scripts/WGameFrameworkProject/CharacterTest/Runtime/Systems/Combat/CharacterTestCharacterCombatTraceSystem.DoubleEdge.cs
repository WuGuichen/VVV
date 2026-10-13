using System;
using MxFramework.Gameplay;
using MxFramework.Combat.Projectiles;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private void OnAccessoryProjectileBorn(CombatProjectileSpawnEvent birth)
        {
            var world = _entityLookup.RuntimeWorld;
            if (world == null) throw new InvalidOperationException("accessory:ProjectileBirthWorldMissing");
            if (world.DoubleEdgeState.Stage == 0) return;
            var p = birth.Projectile;
            if (!world.DoubleEdgeState.CompleteProjectileRequest(birth)) throw new InvalidOperationException("accessory:ProjectileBirthRejected");
            if (!world.DoubleEdgeState.CompleteThirdProjectile(birth)) throw new InvalidOperationException("third-batch:ProjectileBirthRejected");
            if (world.DoubleEdgeState.CompletePendingProjectileActor(birth)) return;
            var source = _entityLookup.FindEntityByCombatId(p.SourceEntityId);
            if (source == null) return;
            if (!world.GameplayComponentWorld.TryGetStore<GameplayAttributeSetComponent>(out var attributes)
                || !attributes.TryGet(source.GameplayEntityId, out var set)) throw new InvalidOperationException("accessory:ProjectileSourceAttributesMissing");
            var values = set.ToArray();
            for (int i = 0; i < values.Length; i++) values[i] = new GameplayAttributeValue(values[i].AttributeId, values[i].BaseValue,
                world.GetCurrentAttribute(source.GameplayEntityId, values[i].AttributeId, includeModifiers: true));
            if (!world.DoubleEdgeState.FreezeProjectileActor(new TacticalCard.TacticalDoubleEdgeBattleState.ProjectileActor(
                p.ProjectileId, p.SpawnSequence, p.SourceEntityId.Value, source.Team.TeamId, source.GameplayEntityId, values)))
                throw new InvalidOperationException("accessory:ProjectileActorBirthRejected");
        }

        private bool EnqueueAccessoryProjectile(in CombatProjectileSpawnRequest request, in TacticalDirectAttackStamp root,
            in TacticalSecondBatchSource parent, long createdFrame, bool direct, in TacticalSecondBatchHitKey receipt = default)
        {
            var owner = _entityLookup.AccessoryState;
            if (owner == null || owner.Stage == 0) { _projectileSpawnRequests.Enqueue(request); return true; }
            if (owner != null && !owner.RememberProjectileRequest(request, root, parent, createdFrame, direct, receipt)) return false;
            var world = _entityLookup.RuntimeWorld; var source = _entityLookup.FindEntityByCombatId(request.SourceEntityId);
            if (direct && source != null && !owner.RememberThirdProjectile(request, owner.ThirdAttack(source.GameplayEntityId, request.ActionInstanceId))) return false;
            TacticalCard.TacticalDoubleEdgeBattleState.ProjectileActor actor = null;
            if (source != null && (!root.IsValid || source.GameplayEntityId == root.Commit.Actor)
                && world.GameplayComponentWorld.TryGetStore<GameplayAttributeSetComponent>(out var attributes)
                && attributes.TryGet(source.GameplayEntityId, out var set)) {
                var values = set.ToArray();
                for (int i = 0; i < values.Length; i++) values[i] = new GameplayAttributeValue(values[i].AttributeId, values[i].BaseValue,
                    world.GetCurrentAttribute(source.GameplayEntityId, values[i].AttributeId, includeModifiers: true));
                actor = new TacticalCard.TacticalDoubleEdgeBattleState.ProjectileActor(0, 0, request.SourceEntityId.Value, source.Team.TeamId, source.GameplayEntityId, values);
            } else actor = owner?.FindAncestorProjectileActor(parent);
            if (actor != null && !owner.RememberProjectileActor(request, actor)) throw new InvalidOperationException("accessory:PendingProjectileActorRejected");
            _projectileSpawnRequests.Enqueue(request); return true;
        }
        private void PublishDoubleEdgeProjectileBirths(CharacterTestCharacterRuntimeWorld world)
        {
            foreach (var birth in _projectileFrameEvents.Spawns)
                if (!world.DoubleEdgeState.CompleteProjectileRequest(birth))
                    throw new InvalidOperationException("accessory:ProjectileBirthRejected");
            world.DoubleEdgeState.ForgetResolvedProjectileRequests(_frame.Value);
        }
    }
}
