using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Gameplay;
using MxFramework.Events;
using MxFramework.Modifiers;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    /// <summary>
    /// EditMode tests for the Gameplay runtime Ability / Entity / Target / Effect core.
    /// All core logic is pure C# with no Unity dependency.
    /// </summary>
    public class AbilitySliceTests
    {
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;
        private const int BuffBurning = 100001;

        private RuntimeEntity CreateEntity(int id, int team, int hp, int attack, int defense)
        {
            var entity = new RuntimeEntity(id, team, AttrHp);
            entity.Store.RegisterAttribute(AttrHp, hp);
            entity.Store.RegisterAttribute(AttrAttack, attack);
            entity.Store.RegisterAttribute(AttrDefense, defense);
            return entity;
        }

        // --- Target Selection ---

        [Test]
        public void Gameplay_TargetSelector_SelectsFirstEnemy()
        {
            var player = CreateEntity(1, 1, 1000, 120, 20);
            var enemy = CreateEntity(2, 2, 600, 80, 10);
            var context = new AbilityContext(player, new IRuntimeEntity[] { player, enemy });

            var selector = new SingleEnemyTargetSelector();
            var targets = selector.SelectTargets(context);

            Assert.AreEqual(1, targets.Count, "Should select exactly one target");
            Assert.AreEqual(2, targets[0].EntityId, "Should select the enemy entity");
        }

        [Test]
        public void SingleEnemyTargetSelector_SkipsDeadEnemies()
        {
            var player = CreateEntity(1, 1, 1000, 120, 20);
            var deadEnemy = CreateEntity(2, 2, 0, 80, 10); // HP = 0 means IsAlive = false
            var context = new AbilityContext(player, new IRuntimeEntity[] { deadEnemy });

            var selector = new SingleEnemyTargetSelector();
            var targets = selector.SelectTargets(context);

            Assert.AreEqual(0, targets.Count, "Should find no alive enemy targets");
        }

        [Test]
        public void SelfTargetSelector_ReturnsCaster()
        {
            var player = CreateEntity(1, 1, 1000, 120, 20);
            var context = new AbilityContext(player, new IRuntimeEntity[] { player });

            var selector = new SelfTargetSelector();
            var targets = selector.SelectTargets(context);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(1, targets[0].EntityId);
        }

        // --- Damage Effect ---

        [Test]
        public void Gameplay_DamageEffect_ReducesTargetHp()
        {
            var player = CreateEntity(1, 1, 1000, 120, 20);
            var enemy = CreateEntity(2, 2, 600, 80, 10);
            var context = new AbilityContext(player, new IRuntimeEntity[] { player, enemy });

            var damage = new DamageEffect(AttrAttack, AttrDefense, AttrHp);
            damage.Apply(context, enemy);

            // damage = max(1, 120 - 10) = 110, HP: 600 -> 490
            Assert.AreEqual(490, enemy.Store.GetAttribute(AttrHp));
        }

        [Test]
        public void DamageEffect_MinimumOneDamage()
        {
            var weakAttacker = CreateEntity(1, 1, 1000, 5, 0);
            var toughTarget = CreateEntity(2, 2, 600, 80, 100);
            var context = new AbilityContext(weakAttacker, new IRuntimeEntity[] { weakAttacker, toughTarget });

            var damage = new DamageEffect(AttrAttack, AttrDefense, AttrHp);
            damage.Apply(context, toughTarget);

            // damage = max(1, 5 - 100) = 1, HP: 600 -> 599
            Assert.AreEqual(599, toughTarget.Store.GetAttribute(AttrHp));
        }

        // --- Apply Buff Effect ---

        [Test]
        public void Gameplay_ApplyBuffEffect_AddsBuff()
        {
            var player = CreateEntity(1, 1, 1000, 120, 20);
            var enemy = CreateEntity(2, 2, 600, 80, 10);
            var context = new AbilityContext(player, new IRuntimeEntity[] { player, enemy });

            var effect = new ApplyBuffEffect(() => new TestBurningBuff());
            effect.Apply(context, enemy);

            Assert.IsTrue(enemy.Buffs.HasBuff(BuffBurning), "Burning buff should be attached");
        }

        // --- Ability Cast ---

        [Test]
        public void Gameplay_AbilityCast_PublishesEventsInOrder()
        {
            var player = CreateEntity(1, 1, 1000, 120, 20);
            var enemy = CreateEntity(2, 2, 600, 80, 10);
            var log = new List<AbilityEventType>();
            player.AbilityEvents.Subscribe(e => log.Add(e.Type));

            var ability = new SimpleAbility(
                1,
                new SingleEnemyTargetSelector(),
                new IAbilityEffect[]
                {
                    new DamageEffect(AttrAttack, AttrDefense, AttrHp)
                });

            var context = new AbilityContext(player, new IRuntimeEntity[] { player, enemy });
            var result = ability.Cast(context);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(4, log.Count, "Should publish 4 events: Started, TargetSelected, EffectApplied, Finished");
            Assert.AreEqual(AbilityEventType.CastStarted, log[0]);
            Assert.AreEqual(AbilityEventType.TargetSelected, log[1]);
            Assert.AreEqual(AbilityEventType.EffectApplied, log[2]);
            Assert.AreEqual(AbilityEventType.CastFinished, log[3]);
        }

        [Test]
        public void Gameplay_AbilityCast_NoEnemy_ReturnsFailure()
        {
            var player = CreateEntity(1, 1, 1000, 120, 20);
            var log = new List<AbilityEventType>();
            player.AbilityEvents.Subscribe(e => log.Add(e.Type));

            var ability = new SimpleAbility(
                1,
                new SingleEnemyTargetSelector(),
                new IAbilityEffect[]
                {
                    new DamageEffect(AttrAttack, AttrDefense, AttrHp)
                });

            // Only caster in candidates - no enemy
            var context = new AbilityContext(player, new IRuntimeEntity[] { player });
            var result = ability.Cast(context);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NoValidTargets", result.FailureReason);
            Assert.AreEqual(AbilityEventType.CastStarted, log[0]);
            Assert.AreEqual(AbilityEventType.CastFailed, log[1]);
        }

        // --- Burning Buff Tick ---

        [Test]
        public void Gameplay_BurningBuff_TicksHp()
        {
            var enemy = CreateEntity(2, 2, 600, 80, 10);
            int hpDelta = 0;
            enemy.Store.OnAttributeChanged.Subscribe(e =>
            {
                if (e.AttributeId == AttrHp)
                    hpDelta += e.Delta;
            });

            var burning = new TestBurningBuff();
            enemy.Buffs.AddBuff(burning, enemy);

            // Tick 1 second
            enemy.Buffs.TickAll(1f);

            // Burning: 35 * 1 layer = 35 damage
            Assert.AreEqual(-35, hpDelta, "Burning should deal 35 damage per tick");
            Assert.AreEqual(565, enemy.Store.GetAttribute(AttrHp), "HP should be 600 - 35 = 565");
        }

        [Test]
        public void Gameplay_RuntimeEntity_IsAliveReflectsHp()
        {
            var entity = CreateEntity(1, 1, 100, 10, 5);
            Assert.IsTrue(entity.IsAlive, "Entity with HP > 0 should be alive");

            entity.Store.AddAttribute(AttrHp, -100, null);
            Assert.IsFalse(entity.IsAlive, "Entity with HP <= 0 should be dead");
        }

        // --- Helper ---

        private class TestBurningBuff : BuffBase
        {
            private const float TickInterval = 1f;
            private float _tickAccumulator;

            public TestBurningBuff()
                : base(id: BuffBurning, duration: 5f, maxLayers: 3) { }

            public override void OnTick(float deltaTime, IBuffTarget target)
            {
                base.OnTick(deltaTime, target);
                _tickAccumulator += deltaTime;
                while (_tickAccumulator >= TickInterval)
                {
                    _tickAccumulator -= TickInterval;
                    target.Attributes.AddAttribute(AttrHp, -35 * CurrentLayers, this);
                }
            }

            public override void OnAttach(IBuffTarget target) { _tickAccumulator = 0f; }
        }
    }
}
