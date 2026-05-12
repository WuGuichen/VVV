using MxFramework.Config;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class ConfigSourceIndexTests
    {
        [Test]
        public void ValidateCrossSourceReferences_WhenGraphKeyExists_Passes()
        {
            ConfigTable<ActionIndexConfig> table = CreateActionIndexTable();
            table.Add(new ActionIndexConfig(1001, 9001));
            var index = new ConfigSourceIndex();
            index.Register(new ConfigSourceEntry(new ConfigSchema("AIActionGraph", null, structureKind: ConfigStructureKind.Graph)).AddKey(9001));

            ConfigTableValidationReport report = index.ValidateCrossSourceReferences(table);

            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void ValidateCrossSourceReferences_WhenGraphKeyMissing_ReportsError()
        {
            ConfigTable<ActionIndexConfig> table = CreateActionIndexTable();
            table.Add(new ActionIndexConfig(1001, 9002));
            var index = new ConfigSourceIndex();
            index.Register(new ConfigSourceEntry(new ConfigSchema("AIActionGraph", null, structureKind: ConfigStructureKind.Graph)).AddKey(9001));

            ConfigTableValidationReport report = index.ValidateCrossSourceReferences(table);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(ConfigError.NotFound, report.Issues[0].Error);
            Assert.AreEqual("GraphId", report.Issues[0].FieldName);
        }

        [Test]
        public void ValidateCrossSourceReferences_WhenSourceMissing_ReportsSchemaError()
        {
            ConfigTable<ActionIndexConfig> table = CreateActionIndexTable();
            table.Add(new ActionIndexConfig(1001, 9001));
            var index = new ConfigSourceIndex();

            ConfigTableValidationReport report = index.ValidateCrossSourceReferences(table);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(ConfigError.TypeNotRegistered, report.Issues[0].Error);
            Assert.AreEqual(0, report.Issues[0].RowId);
        }

        [Test]
        public void SourceEntry_NormalizesLocalizedTextKeys()
        {
            var source = new ConfigSourceEntry(new ConfigSchema("Localization", null, structureKind: ConfigStructureKind.Localization), keyField: "Key")
                .AddKey(new LocalizedTextKey("buff.burn.name"));

            Assert.IsTrue(source.ContainsKey("buff.burn.name"));
        }

        [Test]
        public void AIActionPilotFixture_WhenTableAndGraphReferencesExist_Passes()
        {
            ConfigTable<AIActionIndexFixture> actionIndex = CreateAIActionPilotIndexTable();
            actionIndex.Add(new AIActionIndexFixture(101, "aiaction.synthetic.quick_strike.name", "aiaction.synthetic.quick_strike.desc", true));

            ConfigTable<AIActionGraphFixture> actionGraph = CreateAIActionPilotGraphTable();
            actionGraph.Add(new AIActionGraphFixture(101, "SyntheticQuickStrike", 9001, 10, 1200, "IsNearTarget", "Decrease"));

            ConfigSourceIndex sourceIndex = CreateAIActionPilotSourceIndex(new object[] { 101 }, new object[] { 9001 });

            ConfigTableValidationReport indexReport = sourceIndex.ValidateCrossSourceReferences(actionIndex);
            ConfigTableValidationReport graphReport = sourceIndex.ValidateCrossSourceReferences(actionGraph);

            Assert.IsFalse(indexReport.HasErrors);
            Assert.IsFalse(graphReport.HasErrors);
            Assert.AreEqual(0, indexReport.Issues.Count);
            Assert.AreEqual(0, graphReport.Issues.Count);
        }

        [Test]
        public void AIActionPilotFixture_WhenIndexTargetGraphMissing_ReportsError()
        {
            ConfigTable<AIActionIndexFixture> actionIndex = CreateAIActionPilotIndexTable();
            actionIndex.Add(new AIActionIndexFixture(102, "aiaction.synthetic.missing_graph.name", "aiaction.synthetic.missing_graph.desc", true));

            ConfigSourceIndex sourceIndex = CreateAIActionPilotSourceIndex(new object[] { 101 }, new object[] { 9001 });

            ConfigTableValidationReport report = sourceIndex.ValidateCrossSourceReferences(actionIndex);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(ConfigValidationSeverity.Error, report.Issues[0].Severity);
            Assert.AreEqual(ConfigError.NotFound, report.Issues[0].Error);
            Assert.AreEqual("Id", report.Issues[0].FieldName);
            Assert.AreEqual(102, report.Issues[0].RowId);
        }

        [Test]
        public void AIActionPilotFixture_WhenAbilityGraphMissing_ReportsError()
        {
            ConfigTable<AIActionGraphFixture> actionGraph = CreateAIActionPilotGraphTable();
            actionGraph.Add(new AIActionGraphFixture(101, "SyntheticQuickStrike", 9999, 10, 1200, "IsNearTarget", "Decrease"));

            ConfigSourceIndex sourceIndex = CreateAIActionPilotSourceIndex(new object[] { 101 }, new object[] { 9001 });

            ConfigTableValidationReport report = sourceIndex.ValidateCrossSourceReferences(actionGraph);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(ConfigValidationSeverity.Error, report.Issues[0].Severity);
            Assert.AreEqual(ConfigError.NotFound, report.Issues[0].Error);
            Assert.AreEqual("AbilityId", report.Issues[0].FieldName);
            Assert.AreEqual(101, report.Issues[0].RowId);
        }

        [Test]
        public void ValidateCrossSourceReferences_WhenRuleSeverityWarning_ReportsWarningWithoutBlocking()
        {
            ConfigTable<TransitionBridgeFixture> bridge = CreateTransitionBridgeTable();
            bridge.Add(new TransitionBridgeFixture(1, "LegacyTarget_Stale"));
            var sourceIndex = new ConfigSourceIndex();
            sourceIndex.Register(new ConfigSourceEntry(
                new ConfigSchema("LegacyTarget", null, structureKind: ConfigStructureKind.Graph),
                keyField: "Name").AddKey("LegacyTarget_Active"));

            ConfigTableValidationReport report = sourceIndex.ValidateCrossSourceReferences(bridge);

            Assert.IsFalse(report.HasErrors);
            Assert.AreEqual(1, report.Issues.Count);
            Assert.AreEqual(ConfigValidationSeverity.Warning, report.Issues[0].Severity);
            Assert.AreEqual(ConfigError.NotFound, report.Issues[0].Error);
            Assert.AreEqual("TargetName", report.Issues[0].FieldName);
        }

        private static ConfigTable<ActionIndexConfig> CreateActionIndexTable()
        {
            var schema = new ConfigSchema<ActionIndexConfig>(
                    "AIActionIndex",
                    structureKind: ConfigStructureKind.Table)
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField(
                    "GraphId",
                    ConfigFieldType.ConfigReference,
                    referenceRule: new ConfigReferenceRule("GraphId", "AIActionGraph", ConfigStructureKind.Graph)));

            return new ConfigTable<ActionIndexConfig>(schema);
        }

        private static ConfigTable<AIActionIndexFixture> CreateAIActionPilotIndexTable()
        {
            var schema = new ConfigSchema<AIActionIndexFixture>(
                    "AIActionIndex",
                    structureKind: ConfigStructureKind.Table)
                .AddField(new ConfigField(
                    "Id",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule(
                        "Id",
                        "AIActionGraph",
                        ConfigStructureKind.Graph,
                        severity: ConfigValidationSeverity.Error)))
                .AddField(new ConfigField("NameKey", ConfigFieldType.LocalizedText, required: true))
                .AddField(new ConfigField("DescKey", ConfigFieldType.LocalizedText, required: true))
                .AddField(new ConfigField("CanGet", ConfigFieldType.Boolean, required: true));

            return new ConfigTable<AIActionIndexFixture>(schema);
        }

        private static ConfigTable<AIActionGraphFixture> CreateAIActionPilotGraphTable()
        {
            var schema = new ConfigSchema<AIActionGraphFixture>(
                    "AIActionGraph",
                    structureKind: ConfigStructureKind.Graph)
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField("Name", ConfigFieldType.String, required: true))
                .AddField(new ConfigField(
                    "AbilityId",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule(
                        "AbilityId",
                        "AbilityGraph",
                        ConfigStructureKind.Graph,
                        severity: ConfigValidationSeverity.Error)))
                .AddField(new ConfigField("Cost", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField("CooldownMs", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField("ConditionKey", ConfigFieldType.String, required: true, enumId: "GOAPWorldKey"))
                .AddField(new ConfigField("EffectType", ConfigFieldType.String, required: true, enumId: "GOAPEffectType"));

            return new ConfigTable<AIActionGraphFixture>(schema);
        }

        private static ConfigSourceIndex CreateAIActionPilotSourceIndex(object[] aiActionGraphKeys, object[] abilityGraphKeys)
        {
            var sourceIndex = new ConfigSourceIndex();
            sourceIndex.Register(new ConfigSourceEntry(
                new ConfigSchema("AIActionGraph", null, structureKind: ConfigStructureKind.Graph),
                sourcePath: "ConfigSource/Graphs/AIAction/{id}.json").AddKeys(aiActionGraphKeys));
            sourceIndex.Register(new ConfigSourceEntry(
                new ConfigSchema("AbilityGraph", null, structureKind: ConfigStructureKind.Graph),
                sourcePath: "ConfigSource/Graphs/Ability/{graphKind}/{id}.json").AddKeys(abilityGraphKeys));
            return sourceIndex;
        }

        private static ConfigTable<TransitionBridgeFixture> CreateTransitionBridgeTable()
        {
            var schema = new ConfigSchema<TransitionBridgeFixture>(
                    "TransitionBridge",
                    structureKind: ConfigStructureKind.Table)
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField(
                    "TargetName",
                    ConfigFieldType.ConfigReference,
                    required: true,
                    referenceRule: new ConfigReferenceRule(
                        "TargetName",
                        "LegacyTarget",
                        ConfigStructureKind.Graph,
                        "Name",
                        severity: ConfigValidationSeverity.Warning)));

            return new ConfigTable<TransitionBridgeFixture>(schema);
        }

        private sealed class ActionIndexConfig : IConfigData
        {
            public ActionIndexConfig(int id, int graphId)
            {
                Id = id;
                GraphId = graphId;
            }

            public int Id { get; }
            public int GraphId { get; }
        }

        private sealed class AIActionIndexFixture : IConfigData
        {
            public AIActionIndexFixture(int id, string nameKey, string descKey, bool canGet)
            {
                Id = id;
                NameKey = nameKey;
                DescKey = descKey;
                CanGet = canGet;
            }

            public int Id { get; }
            public string NameKey { get; }
            public string DescKey { get; }
            public bool CanGet { get; }
        }

        private sealed class AIActionGraphFixture : IConfigData
        {
            public AIActionGraphFixture(
                int id,
                string name,
                int abilityId,
                int cost,
                int cooldownMs,
                string conditionKey,
                string effectType)
            {
                Id = id;
                Name = name;
                AbilityId = abilityId;
                Cost = cost;
                CooldownMs = cooldownMs;
                ConditionKey = conditionKey;
                EffectType = effectType;
            }

            public int Id { get; }
            public string Name { get; }
            public int AbilityId { get; }
            public int Cost { get; }
            public int CooldownMs { get; }
            public string ConditionKey { get; }
            public string EffectType { get; }
        }

        private sealed class TransitionBridgeFixture : IConfigData
        {
            public TransitionBridgeFixture(int id, string targetName)
            {
                Id = id;
                TargetName = targetName;
            }

            public int Id { get; }
            public string TargetName { get; }
        }
    }
}
