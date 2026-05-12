using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Gameplay;
using MxFramework.Modifiers;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public class GameplayDiagnosticSnapshotTests
    {
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;
        private const int BuffBurning = 100001;
        private const int ModifierRage = 200001;
        private const int AbilityStrike = 300001;

        [Test]
        public void GameplayDiagnosticSnapshotBuilder_CapturesEntityAttributesBuffsAndModifiers()
        {
            RuntimeEntity entity = CreateEntity(1, 1, 1000, 120, 20);
            entity.Store.AddModifier(new FlatAttributeModifier(900001, AttrAttack, 10));
            entity.Buffs.AddBuff(new TestBurningBuff(), entity);
            entity.Modifiers.AddModifier(new ModifierBase(ModifierRage, paramIndex: 7));

            GameplayDiagnosticSnapshot snapshot = new GameplayDiagnosticSnapshotBuilder().Build(
                "ability-slice",
                "hardcoded SimpleAbility",
                new[] { entity },
                new[] { AttrHp, AttrAttack, AttrDefense },
                AbilityCastResult.Fail("NotCast"),
                new AbilityEvent[0],
                new AttributeChangedEvent[0]);

            GameplayEntitySnapshot entitySnapshot = Single(snapshot.Entities);
            Assert.AreEqual(1, entitySnapshot.EntityId);
            Assert.AreEqual(1, entitySnapshot.TeamId);
            Assert.IsTrue(entitySnapshot.IsAlive);

            GameplayAttributeSnapshot attack = FindAttribute(entitySnapshot.Attributes, AttrAttack);
            Assert.AreEqual(130, attack.FinalValue);

            GameplayBuffSnapshot buff = FindBuff(entitySnapshot.Buffs, BuffBurning);
            Assert.AreEqual(1, buff.CurrentLayers);

            GameplayModifierSnapshot modifier = FindModifier(entitySnapshot.Modifiers, ModifierRage);
            Assert.AreEqual(7, modifier.ParamIndex);
        }

        [Test]
        public void GameplayDiagnosticSnapshotBuilder_CapturesAbilitySourceLastTargetsAndEventOrder()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);
            var abilityEvents = new List<AbilityEvent>
            {
                new AbilityEvent(AbilityEventType.CastStarted, AbilityStrike, player),
                new AbilityEvent(AbilityEventType.TargetSelected, AbilityStrike, player, enemy),
                new AbilityEvent(AbilityEventType.EffectApplied, AbilityStrike, player, enemy),
                new AbilityEvent(AbilityEventType.CastFinished, AbilityStrike, player, enemy),
            };

            GameplayDiagnosticSnapshot snapshot = new GameplayDiagnosticSnapshotBuilder().Build(
                "ability-slice",
                "BasicAbilityConfig -> ConfigAbilityFactory",
                new[] { player, enemy },
                new[] { AttrHp },
                AbilityCastResult.Ok(new IRuntimeEntity[] { enemy }),
                abilityEvents,
                new AttributeChangedEvent[0]);

            Assert.AreEqual("ability-slice", snapshot.SourceName);
            Assert.AreEqual("BasicAbilityConfig -> ConfigAbilityFactory", snapshot.AbilitySource);
            Assert.AreEqual("BasicAbilityConfig -> ConfigAbilityFactory", snapshot.LastCast.AbilitySource);
            Assert.IsTrue(snapshot.LastCastSuccess);

            int targetId = Single(snapshot.LastTargetEntityIds);
            Assert.AreEqual(2, targetId);

            IReadOnlyList<GameplayAbilityEventSnapshot> events = snapshot.AbilityEvents;
            Assert.AreEqual(4, events.Count);
            Assert.AreEqual(AbilityEventType.CastStarted.ToString(), events[0].EventType);
            Assert.AreEqual(AbilityEventType.TargetSelected.ToString(), events[1].EventType);
            Assert.AreEqual(AbilityEventType.EffectApplied.ToString(), events[2].EventType);
            Assert.AreEqual(AbilityEventType.CastFinished.ToString(), events[3].EventType);
            Assert.IsTrue(events[1].TargetEntityId.HasValue);
            Assert.AreEqual(2, events[1].TargetEntityId.Value);
        }

        [Test]
        public void GameplayDiagnosticSnapshotBuilder_CapturesAttributeChangedEvents()
        {
            var source = new TestBurningBuff();
            var attributeEvents = new[]
            {
                new AttributeChangedEvent(AttrHp, 565, 600, 565, source),
            };

            GameplayDiagnosticSnapshot snapshot = new GameplayDiagnosticSnapshotBuilder().Build(
                "ability-slice",
                "hardcoded SimpleAbility",
                new RuntimeEntity[0],
                new[] { AttrHp },
                AbilityCastResult.Fail("NoValidTargets"),
                new AbilityEvent[0],
                attributeEvents);

            GameplayAttributeEventSnapshot evt = Single(snapshot.AttributeEvents);
            Assert.AreEqual(AttrHp, evt.AttributeId);
            Assert.AreEqual(600, evt.OldValue);
            Assert.AreEqual(565, evt.NewValue);
            Assert.AreEqual(-35, evt.Delta);
        }

        [Test]
        public void GameplayDiagnosticSnapshotBuilder_CapturesFailedCastReasonAndEvent()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            var abilityEvents = new List<AbilityEvent>();
            player.AbilityEvents.Subscribe(e => abilityEvents.Add(e));

            var ability = new SimpleAbility(
                AbilityStrike,
                new SingleEnemyTargetSelector(),
                new IAbilityEffect[0]);

            AbilityCastResult result = ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player }));

            GameplayDiagnosticSnapshot snapshot = new GameplayDiagnosticSnapshotBuilder().Build(
                "ability-slice",
                "hardcoded SimpleAbility",
                new[] { player },
                new[] { AttrHp },
                result,
                abilityEvents,
                new AttributeChangedEvent[0]);

            Assert.IsFalse(snapshot.LastCastSuccess);
            Assert.AreEqual("NoValidTargets", snapshot.LastFailureReason);
            Assert.AreEqual(0, snapshot.LastTargetEntityIds.Count);
            Assert.AreEqual(2, snapshot.AbilityEvents.Count);
            Assert.AreEqual(AbilityEventType.CastStarted.ToString(), snapshot.AbilityEvents[0].EventType);
            Assert.AreEqual(AbilityEventType.CastFailed.ToString(), snapshot.AbilityEvents[1].EventType);
            Assert.AreEqual("NoValidTargets", snapshot.AbilityEvents[1].FailureReason);
        }

        [Test]
        public void GameplayDiagnosticSnapshotBuilder_CopiesInputCollections()
        {
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);
            var entities = new List<RuntimeEntity> { player };
            var abilityEvents = new List<AbilityEvent>
            {
                new AbilityEvent(AbilityEventType.CastStarted, AbilityStrike, player)
            };
            var attributeEvents = new List<AttributeChangedEvent>
            {
                new AttributeChangedEvent(AttrHp, 900, 1000, 900, "damage")
            };

            GameplayDiagnosticSnapshot snapshot = new GameplayDiagnosticSnapshotBuilder().Build(
                "ability-slice",
                "hardcoded SimpleAbility",
                entities,
                new[] { AttrHp },
                AbilityCastResult.Ok(new IRuntimeEntity[] { enemy }),
                abilityEvents,
                attributeEvents);

            entities.Add(enemy);
            abilityEvents.Add(new AbilityEvent(AbilityEventType.CastFinished, AbilityStrike, player, enemy));
            attributeEvents.Add(new AttributeChangedEvent(AttrHp, 500, 600, 500, "damage"));

            Assert.AreEqual(1, snapshot.Entities.Count);
            Assert.AreEqual(1, snapshot.AbilityEvents.Count);
            Assert.AreEqual(1, snapshot.AttributeEvents.Count);
            Assert.AreEqual(1, snapshot.LastTargetEntityIds.Count);
            Assert.AreEqual(2, snapshot.LastTargetEntityIds[0]);
        }

        [Test]
        public void GameplayDiagnosticSnapshotBuilder_EmptyInput_ReturnsEmptyCollections()
        {
            GameplayDiagnosticSnapshot snapshot = null;

            Assert.DoesNotThrow(() =>
            {
                snapshot = new GameplayDiagnosticSnapshotBuilder().Build(
                    "empty",
                    string.Empty,
                    null,
                    null,
                    default,
                    null,
                    null);
            });

            Assert.AreEqual(0, snapshot.Entities.Count);
            Assert.AreEqual(0, snapshot.AbilityEvents.Count);
            Assert.AreEqual(0, snapshot.AttributeEvents.Count);
            Assert.AreEqual(0, snapshot.LastTargetEntityIds.Count);
        }

        private static RuntimeEntity CreateEntity(int id, int team, int hp, int attack, int defense)
        {
            var entity = new RuntimeEntity(id, team, AttrHp);
            entity.Store.RegisterAttribute(AttrHp, hp);
            entity.Store.RegisterAttribute(AttrAttack, attack);
            entity.Store.RegisterAttribute(AttrDefense, defense);
            return entity;
        }

        private static T Single<T>(IReadOnlyList<T> list)
        {
            Assert.AreEqual(1, list.Count);
            return list[0];
        }

        private static GameplayAttributeSnapshot FindAttribute(
            IReadOnlyList<GameplayAttributeSnapshot> attributes,
            int attributeId)
        {
            for (int i = 0; i < attributes.Count; i++)
            {
                if (attributes[i].AttributeId == attributeId)
                    return attributes[i];
            }

            Assert.Fail($"Expected attribute snapshot for id {attributeId}.");
            return default;
        }

        private static GameplayBuffSnapshot FindBuff(
            IReadOnlyList<GameplayBuffSnapshot> buffs,
            int buffId)
        {
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].BuffId == buffId)
                    return buffs[i];
            }

            Assert.Fail($"Expected buff snapshot for id {buffId}.");
            return default;
        }

        private static GameplayModifierSnapshot FindModifier(
            IReadOnlyList<GameplayModifierSnapshot> modifiers,
            int modifierId)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].ModifierId == modifierId)
                    return modifiers[i];
            }

            Assert.Fail($"Expected modifier snapshot for id {modifierId}.");
            return default;
        }

        private sealed class FlatAttributeModifier : IAttributeModifier
        {
            private readonly int _value;

            public FlatAttributeModifier(int id, int attributeId, int value)
            {
                Id = id;
                AttributeId = attributeId;
                _value = value;
            }

            public int Id { get; }
            public int AttributeId { get; }
            public AttributeModifierPhase Phase => AttributeModifierPhase.Add;
            public int Priority => 0;

            public int Modify(int currentValue, IAttributeOwner owner)
            {
                return currentValue + _value;
            }
        }

        private sealed class TestBurningBuff : BuffBase
        {
            public TestBurningBuff()
                : base(id: BuffBurning, duration: 5f, maxLayers: 3)
            {
            }
        }
    }
}
