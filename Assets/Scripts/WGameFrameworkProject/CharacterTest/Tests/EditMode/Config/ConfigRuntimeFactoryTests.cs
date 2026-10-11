using MxFramework.Attributes;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Modifiers;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class ConfigRuntimeFactoryTests
    {
        [Test]
        public void ConfigModifierFactory_CreatesConfiguredModifier()
        {
            ConfigTable<BasicModifierConfig> modifiers = CreateModifierTable();
            modifiers.Add(new BasicModifierConfig(
                200001,
                new LocalizedTextKey("mod.power.name"),
                new LocalizedTextKey("mod.power.desc"),
                paramIndex: 2,
                parameters: new[] { 10, 20 }));
            var factory = new ConfigModifierFactory<BasicModifierConfig>(modifiers);

            bool created = factory.TryCreate(200001, out IModifier modifier);

            Assert.IsTrue(created);
            Assert.IsInstanceOf<ConfiguredModifier>(modifier);
            Assert.AreEqual(200001, modifier.Id);
            Assert.AreEqual(2, modifier.ParamIndex);
            Assert.AreEqual(10, ((ConfiguredModifier)modifier).Config.Parameters[0]);
        }

        [Test]
        public void ConfigModifierFactory_WhenConfigMissing_ReturnsFalse()
        {
            var modifierFactory = new ConfigModifierFactory<BasicModifierConfig>(CreateModifierTable());

            Assert.IsFalse(modifierFactory.TryCreate(999999, out IModifier modifier));
            Assert.IsNull(modifier);
        }

        [Test]
        public void ModifierPipeline_CanAddModifierFromConfigFactory()
        {
            ConfigTable<BasicModifierConfig> modifiers = CreateModifierTable();
            modifiers.Add(new BasicModifierConfig(
                200001,
                new LocalizedTextKey("mod.power.name"),
                new LocalizedTextKey("mod.power.desc"),
                paramIndex: 1));
            var owner = new AttributeStore();
            var pipeline = new ModifierPipeline(owner, new ConfigModifierFactory<BasicModifierConfig>(modifiers));

            bool added = pipeline.TryAddModifier(200001, out IModifier modifier);

            Assert.IsTrue(added);
            Assert.AreSame(modifier, pipeline.GetModifier(200001));
            Assert.AreEqual(1, modifier.ParamIndex);
        }

        private static ConfigTable<BasicModifierConfig> CreateModifierTable()
        {
            return new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
        }
    }
}
