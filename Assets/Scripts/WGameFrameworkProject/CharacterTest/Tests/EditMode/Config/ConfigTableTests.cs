using MxFramework.Config;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class ConfigTableTests
    {
        [Test]
        public void ConfigTable_AddAndReadRows()
        {
            ConfigTable<BuffConfig> table = CreateBuffTable();

            table.Add(new BuffConfig(1001, new LocalizedTextKey("buff.burn.name"), 5f, 1, 0));

            Assert.IsTrue(table.TryGetConfig(1001, out BuffConfig config));
            Assert.AreEqual(5f, config.Duration);
            Assert.AreEqual(1, table.Rows.Count);
        }

        [Test]
        public void Validate_WhenIdOutOfRange_ReportsError()
        {
            ConfigTable<BuffConfig> table = CreateBuffTable();
            table.Add(new BuffConfig(3001, new LocalizedTextKey("buff.invalid.name"), 1f, 1, 0));

            ConfigTableValidationReport report = table.Validate();

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(ConfigError.InvalidId, report.Issues[0].Error);
        }

        [Test]
        public void Validate_WhenReferenceMissing_ReportsError()
        {
            ConfigTable<BuffConfig> table = CreateBuffTable();
            table.Add(new BuffConfig(1001, new LocalizedTextKey("buff.burn.name"), 5f, 1, 2001));

            ConfigTableValidationReport report = table.Validate(table);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(ConfigError.NotFound, report.Issues[0].Error);
            Assert.AreEqual("ModifierId", report.Issues[0].FieldName);
        }

        [Test]
        public void Validate_WhenReferenceExists_Passes()
        {
            ConfigTable<BuffConfig> buffTable = CreateBuffTable();
            buffTable.Add(new BuffConfig(1001, new LocalizedTextKey("buff.burn.name"), 5f, 1, 2001));
            var registry = new ConfigRegistry();
            registry.RegisterProvider<BuffConfig>(buffTable);
            var modifiers = new MemoryConfigProvider();
            modifiers.Register(new ModifierConfig(2001));
            registry.RegisterProvider<ModifierConfig>(modifiers);

            ConfigTableValidationReport report = buffTable.Validate(registry);

            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void Validate_WhenLocalizedTextMissing_ReportsLocaleError()
        {
            ConfigTable<BuffConfig> table = CreateBuffTable();
            table.Add(new BuffConfig(1001, new LocalizedTextKey("buff.burn.name"), 5f, 1, 0));
            var localization = new MemoryLocalizationProvider();
            localization.Register(new LocalizedTextKey("buff.burn.name"), LocaleId.ZhCN, "燃烧");

            ConfigTableValidationReport report = table.Validate(localizationProvider: localization);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(ConfigError.NotFound, report.Issues[0].Error);
            Assert.AreEqual("NameText", report.Issues[0].FieldName);
        }

        [Test]
        public void Validate_WhenLocalizedTextExists_Passes()
        {
            ConfigTable<BuffConfig> table = CreateBuffTable();
            table.Add(new BuffConfig(1001, new LocalizedTextKey("buff.burn.name"), 5f, 1, 0));
            var localization = new MemoryLocalizationProvider();
            localization.Register(new LocalizedTextKey("buff.burn.name"), LocaleId.ZhCN, "燃烧");
            localization.Register(new LocalizedTextKey("buff.burn.name"), LocaleId.EnUS, "Burn");

            ConfigTableValidationReport report = table.Validate(localizationProvider: localization);

            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void SchemaExporter_ProducesAiReadableSummary()
        {
            ConfigTable<BuffConfig> table = CreateBuffTable();

            ConfigAiSummary summary = ConfigSchemaExporter.ExportForAi(table.Schema);

            StringAssert.Contains("table: BuffConfig", summary.Text);
            StringAssert.Contains("NameText: LocalizedText", summary.Text);
            StringAssert.Contains("ModifierId: ConfigReference -> Table:ModifierConfig.Id", summary.Text);
            StringAssert.Contains("requiredLocales: zh-CN en-US", summary.Text);
        }

        [Test]
        public void DuplicateFieldName_ReportsSchemaError()
        {
            var schema = new ConfigSchema<BuffConfig>("BuffConfig")
                .AddField(new ConfigField("NameText", ConfigFieldType.LocalizedText))
                .AddField(new ConfigField("NameText", ConfigFieldType.String));
            var table = new ConfigTable<BuffConfig>(schema);

            ConfigTableValidationReport report = table.Validate();

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(ConfigError.DuplicateId, report.Issues[0].Error);
        }

        private static ConfigTable<BuffConfig> CreateBuffTable()
        {
            var schema = new ConfigSchema<BuffConfig>(
                    "BuffConfig",
                    displayName: "Buff Config",
                    description: "Defines generic buff data.",
                    idRange: new ConfigIdRange(1000, 1999))
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField("NameText", ConfigFieldType.LocalizedText, required: true, description: "Localized display name."))
                .AddField(new ConfigField("Duration", ConfigFieldType.Float, required: true))
                .AddField(new ConfigField("MaxLayers", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField(
                    "ModifierId",
                    ConfigFieldType.ConfigReference,
                    referenceRule: new ConfigReferenceRule("ModifierId", typeof(ModifierConfig), required: false)))
                .RequireLocale(LocaleId.ZhCN)
                .RequireLocale(LocaleId.EnUS);

            return new ConfigTable<BuffConfig>(schema);
        }

        private sealed class BuffConfig : IConfigData
        {
            public BuffConfig(int id, LocalizedTextKey nameText, float duration, int maxLayers, int modifierId)
            {
                Id = id;
                NameText = nameText;
                Duration = duration;
                MaxLayers = maxLayers;
                ModifierId = modifierId;
            }

            public int Id { get; }
            public LocalizedTextKey NameText { get; }
            public float Duration { get; }
            public int MaxLayers { get; }
            public int ModifierId { get; }
        }

        private sealed class ModifierConfig : IConfigData
        {
            public ModifierConfig(int id)
            {
                Id = id;
            }

            public int Id { get; }
        }
    }
}
