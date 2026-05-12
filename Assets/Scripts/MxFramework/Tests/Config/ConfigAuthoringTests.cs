using MxFramework.Config;
using MxFramework.Config.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class ConfigAuthoringTests
    {
        [Test]
        public void CreateTemplate_ProducesTsvHeaderAndSample()
        {
            ConfigAuthoringTemplate template = ConfigAuthoring.CreateTemplate(BasicBuffConfig.CreateSchema());

            Assert.AreEqual("BasicBuffConfig", template.TableName);
            StringAssert.Contains("Id\tNameText\tDescriptionText\tDuration\tMaxLayers\tIsPermanent\tModifierId", template.HeaderLine);
            StringAssert.Contains("100000", template.SampleLine);
            StringAssert.Contains("format: tsv", template.Text);
            StringAssert.Contains("ModifierId: ConfigReference -> Table:BasicModifierConfig.Id", template.Text);
            StringAssert.Contains("requiredLocales: zh-CN en-US", template.Text);
        }

        [Test]
        public void CreateTemplate_WhenFieldHasEnumDomain_ExportsEnumId()
        {
            var schema = new ConfigSchema<TestActionConfig>(
                    "TestActionConfig",
                    structureKind: ConfigStructureKind.Table)
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField("WeaponType", ConfigFieldType.Enum, enumId: "weapon.Type"));

            ConfigAuthoringTemplate template = ConfigAuthoring.CreateTemplate(schema);
            ConfigAiSummary summary = ConfigSchemaExporter.ExportForAi(schema);

            StringAssert.Contains("WeaponType: Enum enum=weapon.Type", template.Text);
            StringAssert.Contains("structure: Table", summary.Text);
            StringAssert.Contains("WeaponType: Enum enum=weapon.Type", summary.Text);
        }

        [Test]
        public void CreateTemplate_WhenFieldReferencesGraph_ExportsCrossSourceTarget()
        {
            var schema = new ConfigSchema<TestActionConfig>(
                    "AIActionIndex",
                    structureKind: ConfigStructureKind.Table)
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField(
                    "GraphId",
                    ConfigFieldType.ConfigReference,
                    referenceRule: new ConfigReferenceRule("GraphId", "AIActionGraph", ConfigStructureKind.Graph)));

            ConfigAuthoringTemplate template = ConfigAuthoring.CreateTemplate(schema);
            ConfigAiSummary summary = ConfigSchemaExporter.ExportForAi(schema);

            StringAssert.Contains("GraphId: ConfigReference -> Graph:AIActionGraph.Id", template.Text);
            StringAssert.Contains("GraphId: ConfigReference -> Graph:AIActionGraph.Id", summary.Text);
        }

        [Test]
        public void EnumDomain_DecomposesFlagValues()
        {
            var domain = new ConfigEnumDomain("weapon.Type", isFlags: true)
                .AddValue(new ConfigEnumValue(1, "FistAndLeg", "Fist"))
                .AddValue(new ConfigEnumValue(2, "LongSword", "Sword"))
                .AddValue(new ConfigEnumValue(4, "Gloves", "Gloves"));
            var registry = new ConfigEnumRegistry();

            Assert.IsTrue(registry.Register(domain));
            Assert.IsTrue(registry.TryGet("weapon.Type", out ConfigEnumDomain resolved));
            Assert.IsTrue(resolved.TryDecomposeFlags(3, out var parts, out int unknownBits));
            Assert.AreEqual(0, unknownBits);
            Assert.AreEqual(2, parts.Count);
            Assert.AreEqual("Fist FistAndLeg(1)|Sword LongSword(2)", resolved.FormatValue(3));
        }

        [Test]
        public void ValidateTable_WhenMissingLocalization_ReturnsAuthoringReport()
        {
            ConfigTable<BasicBuffConfig> table = new ConfigTable<BasicBuffConfig>(BasicBuffConfig.CreateSchema());
            table.Add(new BasicBuffConfig(
                100001,
                new LocalizedTextKey("buff.burn.name"),
                new LocalizedTextKey("buff.burn.desc"),
                5f,
                1));

            ConfigAuthoringReport report = ConfigAuthoring.ValidateTable(table, localizationProvider: new MemoryLocalizationProvider());

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual("BasicBuffConfig", report.Issues[0].TableName);
            Assert.AreEqual(100001, report.Issues[0].RowId);
            Assert.AreEqual(ConfigError.NotFound, report.Issues[0].Error);
        }

        [Test]
        public void ExportReportText_IsStableAndAiReadable()
        {
            ConfigTable<BasicBuffConfig> table = new ConfigTable<BasicBuffConfig>(BasicBuffConfig.CreateSchema());
            table.Add(new BasicBuffConfig(
                1,
                default,
                default,
                5f,
                1));

            ConfigAuthoringReport report = ConfigAuthoring.ValidateTable(table);
            string text = ConfigAuthoring.ExportReportText(report);

            StringAssert.Contains("table: BasicBuffConfig", text);
            StringAssert.Contains("hasErrors: true", text);
            StringAssert.Contains("error: InvalidId", text);
            StringAssert.Contains("field: Id", text);
        }

        private sealed class TestActionConfig : IConfigData
        {
            public int Id { get; }
        }
    }
}
