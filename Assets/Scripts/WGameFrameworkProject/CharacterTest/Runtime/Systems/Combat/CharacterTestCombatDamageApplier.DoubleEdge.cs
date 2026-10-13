using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestCombatDamageApplier
    {
        /// <remarks>NoAlloc. Called after classification, before the one existing armor/HP application.</remarks>
        private HitResolveResult ApplyDoubleEdgeScale(CharacterTestCharacterRuntimeWorld world, HitResolveResult result,
            DamageProfileResolveResult payload, CharacterTestAccessoryHitEvidence evidence, int onePunchScale = 1000)
        {
            var state = world.DoubleEdgeState;
            int duel = 1000, shattered = 1000, rear = 1000; decimal glass = 1000, coin = 1000;
            if (TryThirdHit(world, result, payload, evidence, state.ThirdCurrentRound, out var thirdHit)) {
                bool alive = world.GetCurrentAttribute(evidence.Target, CharacterTestCharacterAttributeIds.Hp) > 0;
                if (!state.ApplyThirdDamage(thirdHit.Order, alive, evidence.ActualEnemy, out glass, out coin))
                    throw new System.InvalidOperationException("third-batch:DamageOutsideAcceptedRound");
            }
            onePunchScale = System.Math.Max(1000, onePunchScale);
            bool direct = payload.Success && evidence.Action.IsValid && evidence.Action == payload.Usage.ActionId
                && (evidence.Producer == CharacterTestAccessoryHitProducer.WeaponTrace && payload.Usage.SourceKind == DamageProfileSourceKind.WeaponTrace
                    || evidence.Producer == CharacterTestAccessoryHitProducer.ProjectileDirectSweep && payload.Usage.SourceKind == DamageProfileSourceKind.ProjectileImpact);
            if (state.HasState && !_applyingInteractionDerived && direct) {
                var attacker = FindEntityByCombatId(result.AttackerId); var target = FindEntityByCombatId(result.TargetId);
                var attackerId = evidence.Attacker.IsValid ? evidence.Attacker : attacker?.GameplayEntityId ?? default;
                var targetId = evidence.Target.IsValid ? evidence.Target : target?.GameplayEntityId ?? default;
                if (target != null && target.GameplayEntityId == targetId) {
                    bool attackerAlive = attacker != null && attacker.GameplayEntityId == attackerId
                        && world.GetCurrentAttribute(attackerId, CharacterTestCharacterAttributeIds.Hp) > 0;
                    bool targetAlive = world.GetCurrentAttribute(targetId, CharacterTestCharacterAttributeIds.Hp) > 0;
                    var source = evidence.DoubleEdgeSource;
                    int actualAction = evidence.SourceActionInstance > 0 ? evidence.SourceActionInstance : result.ActionInstanceId;
                    var facts = new TacticalDoubleEdgeDamageFacts(state.Stage, state.Turn, result.Frame.Value,
                        attackerId, targetId, attackerAlive, targetAlive, result.Kind, evidence.Origin, payload.Usage.SourceKind,
                        actualAction, source.IsValid ? source.ActionRevision : evidence.RootOccurrence);
                    state.DamageFactors(facts, source, out duel, out shattered);
                    rear = state.RearScale(facts, evidence.RearWitness);
                }
            }
            decimal combined = (decimal)result.Damage * (duel / 1000m) * (shattered / 1000m) * (rear / 1000m) * (onePunchScale / 1000m);
            combined = System.Math.Min(int.MaxValue, combined * (glass / 1000m));
            combined = System.Math.Min(int.MaxValue, combined * (coin / 1000m));
            int damage = combined >= int.MaxValue ? int.MaxValue : (int)combined;
            return damage == result.Damage ? result : new HitResolveResult(result.AttackerId, result.TargetId,
                result.ActionId, result.ActionInstanceId, result.TraceId, result.Frame, result.Kind, damage,
                result.StaggerFrames, result.Knockback, result.HitPointWorld, result.HitNormalWorld).WithProducerIdentity(result.ProducerIdentity);
        }
    }
}
