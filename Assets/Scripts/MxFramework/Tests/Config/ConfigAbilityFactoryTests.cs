using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Events;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class ConfigAbilityFactoryTests
    {
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;
        private const int BuffBurning = 100001;
        private const int AbilityStrike = 300001;
        private const int AbilityIgnite = 300002;

        [Test]
        public void ConfigAbilityFactory_CreatesDamageAbility()
        {
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(CreateStrikeConfig()));

            bool created = factory.TryCreate(AbilityStrike, out IAbility ability, out string error);

            Assert.IsTrue(created, error);
            Assert.IsInstanceOf<SimpleAbility>(ability);
            Assert.AreEqual(AbilityStrike, ability.AbilityId);
        }

        [Test]
        public void ConfigAbilityFactory_SingleEnemySelector_SelectsEnemyAndDamageReducesHp()
        {
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(CreateStrikeConfig()));
            Assert.IsTrue(factory.TryCreate(AbilityStrike, out IAbility ability, out string error), error);
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);

            AbilityCastResult result = ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Targets.Count);
            Assert.AreEqual(2, result.Targets[0].EntityId);
            Assert.AreEqual(490, enemy.Store.GetAttribute(AttrHp));
        }

        [Test]
        public void ConfigAbilityFactory_CreatesApplyBuffAbility()
        {
            var buffFactory = new TestBuffFactory(BuffBurning);
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(CreateIgniteConfig()), buffFactory);
            Assert.IsTrue(factory.TryCreate(AbilityIgnite, out IAbility ability, out string error), error);
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);

            AbilityCastResult result = ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));

            Assert.IsTrue(result.Success);
            Assert.IsTrue(enemy.Buffs.HasBuff(BuffBurning));
        }

        [Test]
        public void ConfigAbilityFactory_UnknownAbility_ReturnsError()
        {
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(CreateStrikeConfig()));

            bool created = factory.TryCreate(399999, out IAbility ability, out string error);

            Assert.IsFalse(created);
            Assert.IsNull(ability);
            StringAssert.Contains("not found", error);
        }

        [Test]
        public void ConfigAbilityFactory_UnknownEffect_ReturnsError()
        {
            BasicAbilityConfig config = CreateAbility(
                AbilityStrike,
                AbilityTargetSelectorKind.SingleEnemy,
                new AbilityEffectConfig((AbilityEffectKind)999, new[] { 1 }));
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(config));

            bool created = factory.TryCreate(AbilityStrike, out IAbility ability, out string error);

            Assert.IsFalse(created);
            Assert.IsNull(ability);
            StringAssert.Contains("Unsupported ability effect kind", error);
        }

        [Test]
        public void ConfigAbilityFactory_UnknownSelector_ReturnsError()
        {
            BasicAbilityConfig config = CreateAbility(
                AbilityStrike,
                (AbilityTargetSelectorKind)999,
                new AbilityEffectConfig(AbilityEffectKind.DamageByAttackDefense, new[] { AttrAttack, AttrDefense, AttrHp }));
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(config));

            bool created = factory.TryCreate(AbilityStrike, out IAbility ability, out string error);

            Assert.IsFalse(created);
            Assert.IsNull(ability);
            StringAssert.Contains("Unsupported ability target selector kind", error);
        }

        [Test]
        public void ConfigAbilityFactory_MissingParameters_ReturnsError()
        {
            BasicAbilityConfig config = CreateAbility(
                AbilityStrike,
                AbilityTargetSelectorKind.SingleEnemy,
                new AbilityEffectConfig(AbilityEffectKind.DamageByAttackDefense, new[] { AttrAttack, AttrDefense }));
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(config));

            bool created = factory.TryCreate(AbilityStrike, out IAbility ability, out string error);

            Assert.IsFalse(created);
            Assert.IsNull(ability);
            StringAssert.Contains("parameters are insufficient", error);
        }

        [Test]
        public void AbilityEffectConfig_NamedDamageParameters_PopulateLegacyParameters()
        {
            AbilityEffectConfig config = AbilityEffectConfig.DamageByAttackDefense(
                AttrAttack,
                AttrDefense,
                AttrHp);

            Assert.AreEqual(AbilityEffectKind.DamageByAttackDefense, config.Kind);
            Assert.AreEqual(AttrAttack, config.NamedParameters.AttackAttributeId);
            Assert.AreEqual(AttrDefense, config.NamedParameters.DefenseAttributeId);
            Assert.AreEqual(AttrHp, config.NamedParameters.HpAttributeId);
            CollectionAssert.AreEqual(new[] { AttrAttack, AttrDefense, AttrHp }, config.Parameters);
        }

        [Test]
        public void AbilityEffectConfig_LegacyDamageParameters_PopulateNamedParameters()
        {
            var config = new AbilityEffectConfig(
                AbilityEffectKind.DamageByAttackDefense,
                new[] { AttrAttack, AttrDefense, AttrHp });

            Assert.AreEqual(AttrAttack, config.NamedParameters.AttackAttributeId);
            Assert.AreEqual(AttrDefense, config.NamedParameters.DefenseAttributeId);
            Assert.AreEqual(AttrHp, config.NamedParameters.HpAttributeId);
        }

        [Test]
        public void BasicAbilityConfig_CollectReferences_UsesNamedBuffId()
        {
            BasicAbilityConfig config = CreateAbility(
                AbilityIgnite,
                AbilityTargetSelectorKind.SingleEnemy,
                AbilityEffectConfig.ApplyBuff(BuffBurning));
            var references = new List<ConfigReference>();

            config.CollectReferences(references);

            Assert.AreEqual(1, references.Count);
            Assert.AreEqual(BuffBurning, references[0].TargetId);
            Assert.AreEqual("Effects[0].NamedParameters.BuffId", references[0].FieldName);
        }

        [Test]
        public void ConfigAbilityFactory_ApplyBuffWithoutBuffFactory_ReturnsError()
        {
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(CreateIgniteConfig()));

            bool created = factory.TryCreate(AbilityIgnite, out IAbility ability, out string error);

            Assert.IsFalse(created);
            Assert.IsNull(ability);
            StringAssert.Contains("requires an IBuffFactory", error);
        }

        [Test]
        public void ConfigAbilityFactory_ApplyBuffFactoryCannotCreateBuff_ReturnsError()
        {
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(CreateIgniteConfig()), new TestBuffFactory(999999));

            bool created = factory.TryCreate(AbilityIgnite, out IAbility ability, out string error);

            Assert.IsFalse(created);
            Assert.IsNull(ability);
            StringAssert.Contains("failed to create buff", error);
        }

        [Test]
        public void ConfigDrivenAbility_CastPublishesEventsInOrder()
        {
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(CreateStrikeConfig()));
            Assert.IsTrue(factory.TryCreate(AbilityStrike, out IAbility ability, out string error), error);
            RuntimeEntity player = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity enemy = CreateEntity(2, 2, 600, 80, 10);
            var log = new List<AbilityEventType>();
            player.AbilityEvents.Subscribe(e => log.Add(e.Type));

            AbilityCastResult result = ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(
                new[]
                {
                    AbilityEventType.CastStarted,
                    AbilityEventType.TargetSelected,
                    AbilityEventType.EffectApplied,
                    AbilityEventType.CastFinished
                },
                log);
        }

        [Test]
        public void RuntimeAbilitySlice_ConfigDriven_MatchesHardcodedDamage()
        {
            var factory = new ConfigAbilityFactory(CreateAbilityProvider(CreateStrikeConfig()));
            Assert.IsTrue(factory.TryCreate(AbilityStrike, out IAbility configAbility, out string error), error);
            var hardcodedAbility = new SimpleAbility(
                AbilityStrike,
                new SingleEnemyTargetSelector(),
                new IAbilityEffect[] { new DamageEffect(AttrAttack, AttrDefense, AttrHp) });
            RuntimeEntity configPlayer = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity configEnemy = CreateEntity(2, 2, 600, 80, 10);
            RuntimeEntity hardcodedPlayer = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity hardcodedEnemy = CreateEntity(2, 2, 600, 80, 10);

            configAbility.Cast(new AbilityContext(configPlayer, new IRuntimeEntity[] { configPlayer, configEnemy }));
            hardcodedAbility.Cast(new AbilityContext(hardcodedPlayer, new IRuntimeEntity[] { hardcodedPlayer, hardcodedEnemy }));

            Assert.AreEqual(hardcodedEnemy.Store.GetAttribute(AttrHp), configEnemy.Store.GetAttribute(AttrHp));
            Assert.AreEqual(490, configEnemy.Store.GetAttribute(AttrHp));
        }

        private static IConfigProvider CreateAbilityProvider(params BasicAbilityConfig[] configs)
        {
            var table = new ConfigTable<BasicAbilityConfig>(BasicAbilityConfig.CreateSchema());
            for (int i = 0; i < configs.Length; i++)
                table.Add(configs[i]);
            return table;
        }

        private static BasicAbilityConfig CreateStrikeConfig()
        {
            return CreateAbility(
                AbilityStrike,
                AbilityTargetSelectorKind.SingleEnemy,
                AbilityEffectConfig.DamageByAttackDefense(AttrAttack, AttrDefense, AttrHp));
        }

        private static BasicAbilityConfig CreateIgniteConfig()
        {
            return CreateAbility(
                AbilityIgnite,
                AbilityTargetSelectorKind.SingleEnemy,
                AbilityEffectConfig.ApplyBuff(BuffBurning));
        }

        private static BasicAbilityConfig CreateAbility(
            int id,
            AbilityTargetSelectorKind selectorKind,
            params AbilityEffectConfig[] effects)
        {
            return new BasicAbilityConfig(
                id,
                new LocalizedTextKey("ability." + id + ".name"),
                new LocalizedTextKey("ability." + id + ".desc"),
                selectorKind,
                effects);
        }

        private static RuntimeEntity CreateEntity(int id, int team, int hp, int attack, int defense)
        {
            var entity = new RuntimeEntity(id, team, AttrHp);
            entity.Store.RegisterAttribute(AttrHp, hp);
            entity.Store.RegisterAttribute(AttrAttack, attack);
            entity.Store.RegisterAttribute(AttrDefense, defense);
            return entity;
        }

        private sealed class TestBuffFactory : IBuffFactory
        {
            private readonly int _supportedBuffId;

            public TestBuffFactory(int supportedBuffId)
            {
                _supportedBuffId = supportedBuffId;
            }

            public bool TryCreate(int buffId, out IBuff buff)
            {
                if (buffId == _supportedBuffId)
                {
                    buff = new TestBuff(buffId);
                    return true;
                }

                buff = null;
                return false;
            }
        }

        private sealed class TestBuff : BuffBase
        {
            public TestBuff(int id)
                : base(id, duration: 5f, maxLayers: 1)
            {
            }
        }
    }
}
