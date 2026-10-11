using MxFramework.CharacterControl;
using MxFramework.CharacterAction;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestCombatDamageApplier
    {
        private readonly AccessoryMotionSink _accessoryMotionSink = new AccessoryMotionSink();
        private sealed class AccessoryMotionSink : ITacticalAccessoryMotionSink
        {
            internal CharacterTestCharacterRuntimeWorld World;
            internal CharacterTestCharacterEntity Target;
            internal MxFramework.Gameplay.GameplayEntityId Generation;
            public bool TryEnqueue(in CharacterMotionRequest request)
            {
                if (World == null || Target == null || Target.GameplayEntityId != Generation
                    || !World.GameplayComponentWorld.IsAlive(Generation)
                    || World.GetCurrentAttribute(Generation, CharacterTestCharacterAttributeIds.Hp) <= 0
                    || !request.HasValue || request.Kind != CharacterMotionRequestKind.Impulse) return false;
                // EnqueueMotionRequest synchronously appends every HasValue request. The effect owner
                // publishes consumption only after this call returns. No callback runs between admission and publish.
                Target.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ImpulseRaw(request.XRaw, request.YRaw, request.ZRaw,
                    CharacterMovementMode.Idle, request.Source.SourceActionId, request.Source.StableEventId));
                return true;
            }
        }
        private void CommitAccessoryOutcome(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame, HitResolveResult result,
            DamageProfileResolveResult payload, CharacterTestAccessoryHitEvidence evidence, bool targetWasAlive)
        {
            if (_applyingInteractionDerived || evidence.Origin != TacticalAccessoryDamageOrigin.DirectAttack || !payload.Success
                || !evidence.Action.IsValid || evidence.Action != payload.Usage.ActionId || result.Frame.Value != frame.Value
                || (evidence.Producer == CharacterTestAccessoryHitProducer.WeaponTrace && payload.Usage.SourceKind != DamageProfileSourceKind.WeaponTrace)
                || (evidence.Producer == CharacterTestAccessoryHitProducer.ProjectileDirectSweep && payload.Usage.SourceKind != DamageProfileSourceKind.ProjectileImpact)) return;
            var attacker = FindEntityByCombatId(result.AttackerId); var target = FindEntityByCombatId(result.TargetId);
            if (attacker == null || target == null || attacker == target || !targetWasAlive
                || !world.GameplayComponentWorld.IsAlive(attacker.GameplayEntityId)
                || !world.GameplayComponentWorld.IsAlive(target.GameplayEntityId)) return;
            bool attackerAlive = world.GetCurrentAttribute(attacker.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp) > 0;
            bool targetAlive = world.GetCurrentAttribute(target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp) > 0;
            if (!attackerAlive) return;
            // Runtime ID lookup is accepted only after resolving current live actor/generation above.
            world.TryGetTacticalMotion(attacker.Id.Value, out var attackingLedger);
            world.TryGetTacticalMotion(target.Id.Value, out var defendingLedger);
            _accessoryMotionSink.World = world; _accessoryMotionSink.Target = target; _accessoryMotionSink.Generation = target.GameplayEntityId;
            try
            {
                CommitOwner(attacker.InteractionEffects, attackingLedger);
                if (targetAlive) CommitOwner(target.InteractionEffects, defendingLedger);
            }
            finally { _accessoryMotionSink.World = null; _accessoryMotionSink.Target = null; _accessoryMotionSink.Generation = default; }

            void CommitOwner(TacticalInteractionEffectAuthority owner, IMomentumLedger actorLedger)
            {
                if (owner.Stage == 0 || owner.Actor != attacker.GameplayEntityId && owner.Actor != target.GameplayEntityId) return;
                var receipt = new TacticalAccessoryCombatReceipt(owner.Stage, owner.Turn, frame.Value, owner.AccessoryReceiptSequence + 1,
                    attacker.GameplayEntityId, target.GameplayEntityId, evidence.Action, result.ActionInstanceId, result.TraceId,
                    payload.Usage.SourceKind, evidence.Origin, result.Kind, attackerAlive, targetWasAlive, targetAlive, evidence.Direction,
                    target.Physics.EffectiveMassGrams, target.Physics.CanBePushed ? target.Physics.EffectivePushScaleMilli : 0);
                owner.CommitAccessoryReceipt(receipt, actorLedger, defendingLedger, _accessoryMotionSink);
            }
        }
    }
}
