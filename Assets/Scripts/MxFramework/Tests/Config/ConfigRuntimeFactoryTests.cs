using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Modifiers;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class ConfigRuntimeFactoryTests
    {
        [Test]
        public void ConfigBuffFactory_CreatesConfiguredBuff()
        {
            ConfigTable<BasicBuffConfig> buffs = CreateBuffTable();
            buffs.Add(new BasicBuffConfig(
                100001,
                new LocalizedTextKey("buff.burn.name"),
                new LocalizedTextKey("buff.burn.desc"),
                5f,
                3));
            var factory = new ConfigBuffFactory<BasicBuffConfig>(buffs);

            bool created = factory.TryCreate(100001, out IBuff buff);

            Assert.IsTrue(created);
            Assert.IsInstanceOf<ConfiguredBuff>(buff);
            Assert.AreEqual(100001, buff.Id);
            Assert.AreEqual(5f, buff.Duration);
            Assert.AreEqual(3, buff.MaxLayers);
        }

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
        public void Factories_WhenConfigMissing_ReturnFalse()
        {
            var buffFactory = new ConfigBuffFactory<BasicBuffConfig>(CreateBuffTable());
            var modifierFactory = new ConfigModifierFactory<BasicModifierConfig>(CreateModifierTable());

            Assert.IsFalse(buffFactory.TryCreate(999999, out IBuff buff));
            Assert.IsNull(buff);
            Assert.IsFalse(modifierFactory.TryCreate(999999, out IModifier modifier));
            Assert.IsNull(modifier);
        }

        [Test]
        public void BuffPipeline_CanAddBuffFromConfigFactory()
        {
            ConfigTable<BasicBuffConfig> buffs = CreateBuffTable();
            buffs.Add(new BasicBuffConfig(
                100001,
                new LocalizedTextKey("buff.burn.name"),
                new LocalizedTextKey("buff.burn.desc"),
                5f,
                2));
            var pipeline = new BuffPipeline(new ConfigBuffFactory<BasicBuffConfig>(buffs));
            var target = new TestBuffTarget();

            bool added = pipeline.TryAddBuff(100001, target, out IBuff buff);

            Assert.IsTrue(added);
            Assert.IsTrue(pipeline.HasBuff(100001));
            Assert.AreSame(buff, pipeline.GetBuff(100001));
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

        [Test]
        public void BuffConfigReferenceToModifierConfig_ValidatesThroughRegistry()
        {
            ConfigTable<BasicBuffConfig> buffs = CreateBuffTable();
            buffs.Add(new BasicBuffConfig(
                100001,
                new LocalizedTextKey("buff.burn.name"),
                new LocalizedTextKey("buff.burn.desc"),
                5f,
                1,
                modifierId: 200001));
            ConfigTable<BasicModifierConfig> modifiers = CreateModifierTable();
            modifiers.Add(new BasicModifierConfig(
                200001,
                new LocalizedTextKey("mod.power.name"),
                new LocalizedTextKey("mod.power.desc")));
            var registry = new ConfigRegistry();
            registry.RegisterProvider<BasicBuffConfig>(buffs);
            registry.RegisterProvider<BasicModifierConfig>(modifiers);
            var localization = CreateLocalization();

            ConfigTableValidationReport report = buffs.Validate(registry, localization);

            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void BuffConfigReferenceToMissingModifier_ReportsError()
        {
            ConfigTable<BasicBuffConfig> buffs = CreateBuffTable();
            buffs.Add(new BasicBuffConfig(
                100001,
                new LocalizedTextKey("buff.burn.name"),
                new LocalizedTextKey("buff.burn.desc"),
                5f,
                1,
                modifierId: 200001));
            var registry = new ConfigRegistry();
            registry.RegisterProvider<BasicBuffConfig>(buffs);
            registry.RegisterProvider<BasicModifierConfig>(CreateModifierTable());

            ConfigTableValidationReport report = buffs.Validate(registry, CreateLocalization());

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(ConfigError.NotFound, report.Issues[0].Error);
            Assert.AreEqual("ModifierId", report.Issues[0].FieldName);
        }

        private static ConfigTable<BasicBuffConfig> CreateBuffTable()
        {
            return new ConfigTable<BasicBuffConfig>(BasicBuffConfig.CreateSchema());
        }

        private static ConfigTable<BasicModifierConfig> CreateModifierTable()
        {
            return new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
        }

        private static MemoryLocalizationProvider CreateLocalization()
        {
            var localization = new MemoryLocalizationProvider();
            localization.Register(new LocalizedTextKey("buff.burn.name"), LocaleId.ZhCN, "燃烧");
            localization.Register(new LocalizedTextKey("buff.burn.name"), LocaleId.EnUS, "Burn");
            localization.Register(new LocalizedTextKey("buff.burn.desc"), LocaleId.ZhCN, "持续伤害");
            localization.Register(new LocalizedTextKey("buff.burn.desc"), LocaleId.EnUS, "Damage over time");
            localization.Register(new LocalizedTextKey("mod.power.name"), LocaleId.ZhCN, "强化");
            localization.Register(new LocalizedTextKey("mod.power.name"), LocaleId.EnUS, "Power");
            localization.Register(new LocalizedTextKey("mod.power.desc"), LocaleId.ZhCN, "提升效果");
            localization.Register(new LocalizedTextKey("mod.power.desc"), LocaleId.EnUS, "Improve effect");
            return localization;
        }

        private sealed class TestBuffTarget : IBuffTarget
        {
            private readonly MxFramework.Events.EventBus<BuffEvent> _events = new MxFramework.Events.EventBus<BuffEvent>();

            public TestBuffTarget()
            {
                Attributes = new AttributeStore();
                AttributeModifiers = Attributes;
            }

            public AttributeStore Attributes { get; }

            IAttributeOwner IBuffTarget.Attributes => Attributes;

            public IAttributeModifierOwner AttributeModifiers { get; }

            public MxFramework.Events.IEventBus<BuffEvent> BuffEvents => _events;
        }
    }
}
