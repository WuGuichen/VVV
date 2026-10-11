using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public class CombatGameplayEventBridgeTests
    {
        [Test]
        public void DamageResult_ConvertsToEffectAppliedAbilityEvent()
        {
            RuntimeEntity caster = CreateEntity(1);
            RuntimeEntity target = CreateEntity(2);
            HitResolveResult result = Result(HitResolveKind.Damage, damage: 10);

            bool success = CombatGameplayEventBridge.TryCreateAbilityEvent(
                result,
                abilityId: 1001,
                caster,
                target,
                out AbilityEvent abilityEvent);

            Assert.IsTrue(success);
            Assert.AreEqual(AbilityEventType.EffectApplied, abilityEvent.Type);
            Assert.AreEqual(1001, abilityEvent.AbilityId);
            Assert.AreEqual(caster, abilityEvent.Caster);
            Assert.AreEqual(target, abilityEvent.Target);
            Assert.IsNull(abilityEvent.FailureReason);
        }

        [Test]
        public void BlockedResult_ConvertsToFailedAbilityEventWithReason()
        {
            RuntimeEntity caster = CreateEntity(1);
            RuntimeEntity target = CreateEntity(2);
            HitResolveResult result = Result(HitResolveKind.Blocked, damage: 0);

            bool success = CombatGameplayEventBridge.TryCreateAbilityEvent(
                result,
                abilityId: 1001,
                caster,
                target,
                out AbilityEvent abilityEvent);

            Assert.IsFalse(success);
            Assert.AreEqual(AbilityEventType.CastFailed, abilityEvent.Type);
            Assert.AreEqual("Blocked", abilityEvent.FailureReason);
        }

        private static RuntimeEntity CreateEntity(int id)
        {
            var entity = new RuntimeEntity(id, teamId: id, hpAttributeId: 1);
            entity.Store.RegisterAttribute(1, 100);
            return entity;
        }

        private static HitResolveResult Result(HitResolveKind kind, int damage)
        {
            return new HitResolveResult(
                new CombatEntityId(1),
                new CombatEntityId(2),
                actionId: 1001,
                actionInstanceId: 2001,
                traceId: 7,
                frame: new CombatFrame(10),
                kind,
                damage,
                staggerFrames: 0,
                knockback: FixVector3.Zero);
        }
    }
}
