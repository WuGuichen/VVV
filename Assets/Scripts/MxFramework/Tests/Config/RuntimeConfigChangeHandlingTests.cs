using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;
using MxFramework.Modifiers;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class RuntimeConfigChangeHandlingTests
    {
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;
        private const int BuffBurning = 100001;
        private const int ModifierAttack = 200001;
        private const int AbilityStrike = 300001;

        [Test]
        public void RuntimeAbilityConfigResolver_ConfigChange_RebuildsNewAbilityButDoesNotHotSwapExistingAbility()
        {
            var provider = new MemoryConfigProvider(ConfigDuplicatePolicy.Replace);
            provider.Register(CreateDamageAbility(AttrAttack, AttrDefense, AttrHp));
            var resolver = new RuntimeAbilityConfigResolver(provider, sourceName: "base");
            Assert.IsTrue(resolver.TryCreate(AbilityStrike, out IAbility oldAbility, out string error), error);

            provider.Register(CreateDamageAbility(AttrDefense, AttrDefense, AttrHp));
            Assert.IsTrue(resolver.TryCreate(AbilityStrike, out IAbility rebuiltAbility, out error), error);

            RuntimeEntity oldPlayer = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity oldEnemy = CreateEntity(2, 2, 600, 80, 10);
            RuntimeEntity newPlayer = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity newEnemy = CreateEntity(2, 2, 600, 80, 10);

            oldAbility.Cast(new AbilityContext(oldPlayer, new IRuntimeEntity[] { oldPlayer, oldEnemy }));
            rebuiltAbility.Cast(new AbilityContext(newPlayer, new IRuntimeEntity[] { newPlayer, newEnemy }));

            Assert.AreEqual(490, oldEnemy.Store.GetAttribute(AttrHp));
            Assert.AreEqual(590, newEnemy.Store.GetAttribute(AttrHp));
        }

        [Test]
        public void ConfiguredBuff_ConfigChange_DoesNotRetroactivelyMutateAttachedBuff()
        {
            var provider = new MemoryConfigProvider(ConfigDuplicatePolicy.Replace);
            provider.Register(CreateBuff(duration: 5f));
            var factory = new ConfigBuffFactory<BasicBuffConfig>(provider);
            RuntimeEntity entity = CreateEntity(1, 1, 1000, 120, 20);

            Assert.IsTrue(factory.TryCreate(BuffBurning, out IBuff attachedBuff));
            entity.Buffs.AddBuff(attachedBuff, entity);
            provider.Register(CreateBuff(duration: 10f));
            Assert.IsTrue(factory.TryCreate(BuffBurning, out IBuff newBuff));

            BuffSnapshot[] existing = entity.Buffs.CreateSnapshot();
            Assert.AreEqual(1, existing.Length);
            Assert.AreEqual(5f, existing[0].Duration);
            Assert.AreEqual(10f, newBuff.Duration);
        }

        [Test]
        public void ConfiguredModifier_ConfigChange_DoesNotRetroactivelyMutateAttachedModifier()
        {
            var provider = new MemoryConfigProvider(ConfigDuplicatePolicy.Replace);
            provider.Register(CreateModifier(addValue: 10));
            var factory = new ConfigModifierFactory<BasicModifierConfig>(provider);
            RuntimeEntity oldEntity = CreateEntity(1, 1, 1000, 120, 20);
            RuntimeEntity newEntity = CreateEntity(2, 1, 1000, 120, 20);

            Assert.IsTrue(factory.TryCreate(ModifierAttack, out IModifier oldModifier));
            oldEntity.Modifiers.AddModifier(oldModifier);
            provider.Register(CreateModifier(addValue: 30));
            Assert.IsTrue(factory.TryCreate(ModifierAttack, out IModifier newModifier));
            newEntity.Modifiers.AddModifier(newModifier);

            oldEntity.Modifiers.ApplyAll(null);
            newEntity.Modifiers.ApplyAll(null);

            Assert.AreEqual(130, oldEntity.Store.GetAttribute(AttrAttack));
            Assert.AreEqual(150, newEntity.Store.GetAttribute(AttrAttack));
        }

        [Test]
        public void RuntimeAbilityConfigResolver_CreateSummary_IncludesSourcePolicyChangesAndResults()
        {
            var changeSet = new ConfigChangeSet();
            changeSet.Add(new ConfigRowChange(
                typeof(BasicAbilityConfig),
                AbilityStrike,
                ConfigLayerKind.Patch,
                ConfigPatchOperation.Upsert,
                ConfigMergeChangeKind.Replaced,
                "hotfix"));
            changeSet.Add(new ConfigRowChange(
                typeof(BasicBuffConfig),
                BuffBurning,
                ConfigLayerKind.Patch,
                ConfigPatchOperation.Upsert,
                ConfigMergeChangeKind.Replaced,
                "hotfix"));

            var resolver = new RuntimeAbilityConfigResolver(
                new MemoryConfigProvider(),
                sourceName: "runtime-test",
                changeSet: changeSet);
            resolver.ChangeSummary.AddRebuiltAbility(AbilityStrike);
            resolver.ChangeSummary.AddFailedAbility(AbilityStrike + 1, "missing");

            string summary = resolver.CreateSummary();

            StringAssert.Contains("source=runtime-test", summary);
            StringAssert.Contains("policy=RebuildOnResolve", summary);
            StringAssert.Contains("changed abilities=1", summary);
            StringAssert.Contains("buffs=1", summary);
            StringAssert.Contains("modifiers=0", summary);
            StringAssert.Contains("rebuilt=1", summary);
            StringAssert.Contains("failed=1", summary);
            StringAssert.Contains("missing", summary);
        }

        [Test]
        public void RuntimeAbilityConfigResolver_MissingConfig_ReturnsPolicyAwareError()
        {
            var resolver = new RuntimeAbilityConfigResolver(new MemoryConfigProvider(), sourceName: "empty");

            bool created = resolver.TryCreate(AbilityStrike, out IAbility ability, out string error);

            Assert.IsFalse(created);
            Assert.IsNull(ability);
            StringAssert.Contains("Ability rebuild failed", error);
            StringAssert.Contains("policy=RebuildOnResolve", error);
            StringAssert.Contains("not found", error);
        }

        private static BasicAbilityConfig CreateDamageAbility(int attackAttr, int defenseAttr, int hpAttr)
        {
            return new BasicAbilityConfig(
                AbilityStrike,
                new LocalizedTextKey("ability.strike.name"),
                new LocalizedTextKey("ability.strike.desc"),
                AbilityTargetSelectorKind.SingleEnemy,
                new[] { AbilityEffectConfig.DamageByAttackDefense(attackAttr, defenseAttr, hpAttr) });
        }

        private static BasicBuffConfig CreateBuff(float duration)
        {
            return new BasicBuffConfig(
                BuffBurning,
                new LocalizedTextKey("buff.burn.name"),
                new LocalizedTextKey("buff.burn.desc"),
                duration,
                maxLayers: 1);
        }

        private static BasicModifierConfig CreateModifier(int addValue)
        {
            return new BasicModifierConfig(
                ModifierAttack,
                new LocalizedTextKey("modifier.attack.name"),
                new LocalizedTextKey("modifier.attack.desc"),
                AttrAttack,
                new[] { addValue });
        }

        private static RuntimeEntity CreateEntity(int id, int team, int hp, int attack, int defense)
        {
            var entity = new RuntimeEntity(id, team, AttrHp);
            entity.Store.RegisterAttribute(AttrHp, hp);
            entity.Store.RegisterAttribute(AttrAttack, attack);
            entity.Store.RegisterAttribute(AttrDefense, defense);
            return entity;
        }
    }
}
