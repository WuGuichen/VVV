using System;
using System.Collections.Generic;
using MxFramework.Config;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public sealed class AIActionDryRunFieldMapTests
    {
        [Test]
        public void DryRunFieldMap_RequiredTableFields_MapToAIActionIndex()
        {
            AIActionDryRunSource source = CreateValidSource();

            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(source);

            Assert.AreEqual(1, result.IndexRows.Count);
            Assert.AreEqual(101, result.IndexRows[0].Id);
            Assert.AreEqual("aiaction.synthetic.alpha.name", result.IndexRows[0].NameKey.Value);
            Assert.AreEqual("aiaction.synthetic.alpha.desc", result.IndexRows[0].DescKey.Value);
            Assert.IsTrue(result.IndexRows[0].CanGet);
        }

        [Test]
        public void DryRunFieldMap_RequiredGraphFields_MapToAIActionGraph()
        {
            AIActionDryRunSource source = CreateValidSource();

            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(source);

            Assert.AreEqual(1, result.Graphs.Count);
            Assert.AreEqual(101, result.Graphs[0].Id);
            Assert.AreEqual("SyntheticAlphaAction", result.Graphs[0].Name);
            Assert.AreEqual(9001, result.Graphs[0].AbilityId);
            Assert.AreEqual(7, result.Graphs[0].Cost);
            Assert.AreEqual(500, result.Graphs[0].CooldownMs);
            Assert.AreEqual("HasSyntheticTarget", result.Graphs[0].Conditions[0].Key);
            Assert.AreEqual("Equal", result.Graphs[0].Conditions[0].Compare);
            Assert.AreEqual("true", result.Graphs[0].Conditions[0].Value);
            Assert.AreEqual("SyntheticThreat", result.Graphs[0].Effects[0].Key);
            Assert.AreEqual("Increase", result.Graphs[0].Effects[0].Effect);
        }

        [Test]
        public void DryRunFieldMap_AbilityGraphEvidence_IsPreservedAsKeysOnly()
        {
            AIActionDryRunSource source = CreateValidSource();

            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(source);

            Assert.AreEqual(1, result.AbilityGraphKeys.Count);
            Assert.AreEqual(9001, result.AbilityGraphKeys[0].Id);
            Assert.AreEqual("synthetic://ability/9001", result.AbilityGraphKeys[0].EvidencePath);
        }

        [Test]
        public void DryRunFieldMap_UnsupportedSourceFields_AreStructuredReportEntries()
        {
            AIActionDryRunSource source = CreateValidSource();
            source.IndexFields.Add("legacyIcon", "synthetic-icon-placeholder");
            source.GraphFields.Add("legacyShortPayload", new[] { "synthetic", "unsupported" });

            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(source);

            Assert.AreEqual(2, result.Report.UnsupportedFields.Count);
            AssertUnsupported(result.Report.UnsupportedFields, "AIActionIndex", "legacyIcon", "AIActionIndex");
            AssertUnsupported(result.Report.UnsupportedFields, "AIActionGraph", "legacyShortPayload", "AIActionGraph");
        }

        [Test]
        public void DryRunFieldMap_ZeroAbilityId_IsSentinelCandidate()
        {
            AIActionDryRunSource source = CreateValidSource();
            source.GraphFields["abilityId"] = 0;

            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(source);

            Assert.AreEqual(0, result.Graphs[0].AbilityId);
            Assert.IsTrue(result.Report.HasSentinelCandidates);
            Assert.AreEqual(1, result.Report.SentinelCandidates.Count);
            Assert.AreEqual("AIActionGraph", result.Report.SentinelCandidates[0].SourceSection);
            Assert.AreEqual("abilityId", result.Report.SentinelCandidates[0].FieldName);
            Assert.AreEqual("0", result.Report.SentinelCandidates[0].RawValue);
            Assert.AreEqual("Requires explicit sentinel rule before reference validation can downgrade it.", result.Report.SentinelCandidates[0].Reason);
        }

        [Test]
        public void DryRunFieldMap_DoesNotEmitSourceFilesOrRuntimeBytes()
        {
            AIActionDryRunSource source = CreateValidSource();

            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(source);

            Assert.AreEqual(0, result.Report.GeneratedSourceFiles.Count);
            Assert.IsFalse(result.Report.RuntimeBytesGenerated);
        }

        [Test]
        public void DryRunReferenceReport_WhenAllDryRunKeysExist_PassesWithRegisteredKeys()
        {
            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(CreateValidSource());

            AIActionDryRunReport report = AIActionDryRunReferenceReportFixture.Build(
                result,
                CreateValidLocalizationKeys());

            Assert.IsFalse(report.HasErrors);
            Assert.AreEqual(0, report.ErrorCount);
            Assert.AreEqual(0, report.WarningCount);
            Assert.AreEqual(1, report.InfoCount);
            AssertRegistration(report.SourceRegistrations, "Table:AIActionIndex", 1);
            AssertRegistration(report.SourceRegistrations, "Graph:AIActionGraph", 1);
            AssertRegistration(report.SourceRegistrations, "Graph:AbilityGraph", 1);
        }

        [Test]
        public void DryRunReferenceReport_WhenIndexGraphMissing_ReportsError()
        {
            AIActionDryRunSource source = CreateValidSource();
            source.IndexFields["id"] = 102;
            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(source);

            AIActionDryRunReport report = AIActionDryRunReferenceReportFixture.Build(
                result,
                CreateValidLocalizationKeys());

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(1, report.ErrorCount);
            AIActionDryRunReportEntry entry = FindEntry(report.ValidationEntries, "pilot02.aiaction.index.graph");
            Assert.AreEqual(ConfigValidationSeverity.Error, entry.Severity);
            Assert.AreEqual(ConfigError.NotFound, entry.Error);
            Assert.AreEqual("AIActionIndex", entry.SourceSection);
            Assert.AreEqual(102, entry.RowId);
            Assert.AreEqual("Id", entry.FieldName);
            Assert.AreEqual("Graph:AIActionGraph", entry.TargetSourceId);
        }

        [Test]
        public void DryRunReferenceReport_WhenAbilityGraphMissing_ReportsError()
        {
            AIActionDryRunSource source = CreateValidSource();
            source.AbilityGraphKeys.Clear();
            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(source);

            AIActionDryRunReport report = AIActionDryRunReferenceReportFixture.Build(
                result,
                CreateValidLocalizationKeys());

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(1, report.ErrorCount);
            AIActionDryRunReportEntry entry = FindEntry(report.ValidationEntries, "pilot02.aiaction.ability");
            Assert.AreEqual(ConfigValidationSeverity.Error, entry.Severity);
            Assert.AreEqual(ConfigError.NotFound, entry.Error);
            Assert.AreEqual("AIActionGraph", entry.SourceSection);
            Assert.AreEqual(101, entry.RowId);
            Assert.AreEqual("AbilityId", entry.FieldName);
            Assert.AreEqual("Graph:AbilityGraph", entry.TargetSourceId);
        }

        [Test]
        public void DryRunReferenceReport_WhenLocalizationMissing_IsWarningOnly()
        {
            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(CreateValidSource());

            AIActionDryRunReport report = AIActionDryRunReferenceReportFixture.Build(
                result,
                Array.Empty<LocalizedTextKey>());

            Assert.IsFalse(report.HasErrors);
            Assert.AreEqual(0, report.ErrorCount);
            Assert.AreEqual(2, report.WarningCount);
            Assert.AreEqual(1, report.InfoCount);
            AIActionDryRunReportEntry nameEntry = FindEntry(report.ValidationEntries, "pilot02.aiaction.localization", "NameKey");
            AIActionDryRunReportEntry descEntry = FindEntry(report.ValidationEntries, "pilot02.aiaction.localization", "DescKey");
            Assert.AreEqual(ConfigValidationSeverity.Warning, nameEntry.Severity);
            Assert.AreEqual(ConfigValidationSeverity.Warning, descEntry.Severity);
            Assert.AreEqual("Localization:Localization", nameEntry.TargetSourceId);
            Assert.AreEqual("Localization:Localization", descEntry.TargetSourceId);
        }

        [Test]
        public void DryRunReferenceReport_RuntimeBytesBoundary_IsInfoOnly()
        {
            AIActionDryRunResult result = AIActionDryRunFieldMapFixture.Map(CreateValidSource());

            AIActionDryRunReport report = AIActionDryRunReferenceReportFixture.Build(
                result,
                CreateValidLocalizationKeys());

            Assert.IsFalse(report.RuntimeBytesGenerated);
            Assert.AreEqual(0, report.GeneratedSourceFiles.Count);
            Assert.AreEqual(0, report.ErrorCount);
            Assert.AreEqual(0, report.WarningCount);
            Assert.AreEqual(1, report.InfoCount);
            AIActionDryRunReportEntry entry = FindEntry(report.ValidationEntries, "pilot02.aiaction.runtime-bytes");
            Assert.AreEqual(ConfigValidationSeverity.Info, entry.Severity);
            Assert.AreEqual(ConfigError.None, entry.Error);
            Assert.AreEqual("RuntimeBytesGenerated", entry.FieldName);
        }

        private static AIActionDryRunSource CreateValidSource()
        {
            var source = new AIActionDryRunSource();
            source.IndexFields.Add("id", 101);
            source.IndexFields.Add("nameKey", "aiaction.synthetic.alpha.name");
            source.IndexFields.Add("descKey", "aiaction.synthetic.alpha.desc");
            source.IndexFields.Add("canGet", true);

            source.GraphFields.Add("id", 101);
            source.GraphFields.Add("name", "SyntheticAlphaAction");
            source.GraphFields.Add("abilityId", 9001);
            source.GraphFields.Add("cost", 7);
            source.GraphFields.Add("cooldownMs", 500);
            source.GraphFields.Add(
                "conditions",
                new[]
                {
                    new AIActionDryRunCondition("HasSyntheticTarget", "Equal", "true")
                });
            source.GraphFields.Add(
                "effects",
                new[]
                {
                    new AIActionDryRunEffect("SyntheticThreat", "Increase")
                });

            source.AbilityGraphKeys.Add(new AbilityGraphKeyEvidence(9001, "synthetic://ability/9001"));
            return source;
        }

        private static IReadOnlyList<LocalizedTextKey> CreateValidLocalizationKeys()
        {
            return new[]
            {
                new LocalizedTextKey("aiaction.synthetic.alpha.name"),
                new LocalizedTextKey("aiaction.synthetic.alpha.desc")
            };
        }

        private static void AssertUnsupported(
            IReadOnlyList<AIActionUnsupportedFieldReportEntry> entries,
            string section,
            string sourceField,
            string destinationShape)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].SourceSection == section &&
                    entries[i].SourceField == sourceField &&
                    entries[i].DestinationShape == destinationShape)
                {
                    return;
                }
            }

            Assert.Fail("Expected unsupported field report entry: " + section + "." + sourceField);
        }

        private static void AssertRegistration(
            IReadOnlyList<AIActionDryRunSourceRegistration> registrations,
            string sourceId,
            int keyCount)
        {
            for (int i = 0; i < registrations.Count; i++)
            {
                if (registrations[i].SourceId == sourceId)
                {
                    Assert.AreEqual(keyCount, registrations[i].KeyCount);
                    return;
                }
            }

            Assert.Fail("Expected dry-run source registration: " + sourceId);
        }

        private static AIActionDryRunReportEntry FindEntry(
            IReadOnlyList<AIActionDryRunReportEntry> entries,
            string ruleId,
            string fieldName = "")
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].RuleId == ruleId &&
                    (string.IsNullOrEmpty(fieldName) || entries[i].FieldName == fieldName))
                {
                    return entries[i];
                }
            }

            Assert.Fail("Expected dry-run report entry: " + ruleId + " " + fieldName);
            return default;
        }

        private static class AIActionDryRunFieldMapFixture
        {
            private static readonly HashSet<string> SupportedIndexFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "id",
                "nameKey",
                "descKey",
                "canGet"
            };

            private static readonly HashSet<string> SupportedGraphFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "id",
                "name",
                "abilityId",
                "cost",
                "cooldownMs",
                "conditions",
                "effects"
            };

            public static AIActionDryRunResult Map(AIActionDryRunSource source)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));

                var report = new AIActionDryRunReport();
                ReportUnsupportedFields("AIActionIndex", "AIActionIndex", source.IndexFields, SupportedIndexFields, report);
                ReportUnsupportedFields("AIActionGraph", "AIActionGraph", source.GraphFields, SupportedGraphFields, report);

                var indexRows = new List<AIActionIndexDryRunRow>
                {
                    new AIActionIndexDryRunRow(
                        ReadInt(source.IndexFields, "id"),
                        new LocalizedTextKey(ReadString(source.IndexFields, "nameKey")),
                        new LocalizedTextKey(ReadString(source.IndexFields, "descKey")),
                        ReadBool(source.IndexFields, "canGet"))
                };

                int abilityId = ReadInt(source.GraphFields, "abilityId");
                object rawAbilityId = ReadRaw(source.GraphFields, "abilityId");
                if (IsAbilitySentinel(rawAbilityId, abilityId))
                {
                    report.SentinelCandidates.Add(new AIActionSentinelCandidateReportEntry(
                        "AIActionGraph",
                        "abilityId",
                        FormatRawValue(rawAbilityId),
                        "Requires explicit sentinel rule before reference validation can downgrade it."));
                }

                var graphs = new List<AIActionGraphDryRunModel>
                {
                    new AIActionGraphDryRunModel(
                        ReadInt(source.GraphFields, "id"),
                        ReadString(source.GraphFields, "name"),
                        abilityId,
                        ReadInt(source.GraphFields, "cost"),
                        ReadInt(source.GraphFields, "cooldownMs"),
                        ReadConditions(source.GraphFields),
                        ReadEffects(source.GraphFields))
                };

                return new AIActionDryRunResult(indexRows, graphs, source.AbilityGraphKeys, report);
            }

            private static void ReportUnsupportedFields(
                string section,
                string destinationShape,
                Dictionary<string, object> fields,
                HashSet<string> supportedFields,
                AIActionDryRunReport report)
            {
                foreach (KeyValuePair<string, object> field in fields)
                {
                    if (!supportedFields.Contains(field.Key))
                    {
                        report.UnsupportedFields.Add(new AIActionUnsupportedFieldReportEntry(
                            section,
                            field.Key,
                            destinationShape,
                            "No Pilot03 normalized destination field."));
                    }
                }
            }

            private static IReadOnlyList<AIActionDryRunCondition> ReadConditions(Dictionary<string, object> fields)
            {
                object value = ReadRaw(fields, "conditions");
                return value as IReadOnlyList<AIActionDryRunCondition> ?? Array.Empty<AIActionDryRunCondition>();
            }

            private static IReadOnlyList<AIActionDryRunEffect> ReadEffects(Dictionary<string, object> fields)
            {
                object value = ReadRaw(fields, "effects");
                return value as IReadOnlyList<AIActionDryRunEffect> ?? Array.Empty<AIActionDryRunEffect>();
            }

            private static object ReadRaw(Dictionary<string, object> fields, string fieldName)
            {
                fields.TryGetValue(fieldName, out object value);
                return value;
            }

            private static int ReadInt(Dictionary<string, object> fields, string fieldName)
            {
                object value = ReadRaw(fields, fieldName);
                if (value is int intValue)
                    return intValue;

                if (value is string text && int.TryParse(text, out int parsed))
                    return parsed;

                return 0;
            }

            private static string ReadString(Dictionary<string, object> fields, string fieldName)
            {
                object value = ReadRaw(fields, fieldName);
                return value == null ? string.Empty : value.ToString();
            }

            private static bool ReadBool(Dictionary<string, object> fields, string fieldName)
            {
                object value = ReadRaw(fields, fieldName);
                if (value is bool boolValue)
                    return boolValue;

                return value is string text && bool.TryParse(text, out bool parsed) && parsed;
            }

            private static bool IsAbilitySentinel(object rawValue, int abilityId)
            {
                if (rawValue == null)
                    return true;

                if (abilityId <= 0)
                    return true;

                if (rawValue is string text)
                {
                    return string.IsNullOrWhiteSpace(text) ||
                           string.Equals(text, "none", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(text, "null", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(text, "sentinel", StringComparison.OrdinalIgnoreCase);
                }

                return false;
            }

            private static string FormatRawValue(object rawValue)
            {
                return rawValue == null ? string.Empty : rawValue.ToString();
            }
        }

        private static class AIActionDryRunReferenceReportFixture
        {
            private static readonly HashSet<string> KnownConditionKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "HasSyntheticTarget"
            };

            private static readonly HashSet<string> KnownConditionCompares = new HashSet<string>(StringComparer.Ordinal)
            {
                "Equal"
            };

            private static readonly HashSet<string> KnownEffectTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "Increase"
            };

            public static AIActionDryRunReport Build(
                AIActionDryRunResult result,
                IEnumerable<LocalizedTextKey> localizationKeys)
            {
                if (result == null)
                    throw new ArgumentNullException(nameof(result));

                ConfigSourceIndex sourceIndex = CreateSourceIndex(result, localizationKeys);

                ConfigTable<AIActionIndexDryRunRow> indexTable = CreateIndexTable();
                indexTable.RegisterRange(result.IndexRows);
                ConfigTable<AIActionGraphDryRunModel> graphTable = CreateGraphTable();
                graphTable.RegisterRange(result.Graphs);

                AddValidationIssues(result.Report, sourceIndex.ValidateCrossSourceReferences(indexTable));
                AddValidationIssues(result.Report, sourceIndex.ValidateCrossSourceReferences(graphTable));
                AddEnumDomainWarnings(result.Report, result.Graphs);
                result.Report.ValidationEntries.Add(new AIActionDryRunReportEntry(
                    ConfigValidationSeverity.Info,
                    "pilot02.aiaction.runtime-bytes",
                    ConfigError.None,
                    "AIActionDryRun",
                    0,
                    "RuntimeBytesGenerated",
                    "RuntimeBytes",
                    "Runtime bytes are not generated in Pilot04 dry-run."));

                return result.Report;
            }

            private static ConfigSourceIndex CreateSourceIndex(
                AIActionDryRunResult result,
                IEnumerable<LocalizedTextKey> localizationKeys)
            {
                var sourceIndex = new ConfigSourceIndex();

                ConfigSourceEntry indexSource = new ConfigSourceEntry(
                    new ConfigSchema("AIActionIndex", null, structureKind: ConfigStructureKind.Table),
                    sourcePath: "ConfigSource/Tables/AIActionIndex.tsv");
                for (int i = 0; i < result.IndexRows.Count; i++)
                    indexSource.AddKey(result.IndexRows[i].Id);
                Register(sourceIndex, result.Report, indexSource);

                ConfigSourceEntry graphSource = new ConfigSourceEntry(
                    new ConfigSchema("AIActionGraph", null, structureKind: ConfigStructureKind.Graph),
                    sourcePath: "ConfigSource/Graphs/AIAction/{id}.json");
                for (int i = 0; i < result.Graphs.Count; i++)
                    graphSource.AddKey(result.Graphs[i].Id);
                Register(sourceIndex, result.Report, graphSource);

                ConfigSourceEntry abilitySource = new ConfigSourceEntry(
                    new ConfigSchema("AbilityGraph", null, structureKind: ConfigStructureKind.Graph),
                    sourcePath: "ConfigSource/Graphs/Ability/{graphKind}/{id}.json");
                for (int i = 0; i < result.AbilityGraphKeys.Count; i++)
                    abilitySource.AddKey(result.AbilityGraphKeys[i].Id);
                Register(sourceIndex, result.Report, abilitySource);

                ConfigSourceEntry localizationSource = new ConfigSourceEntry(
                    new ConfigSchema("Localization", null, structureKind: ConfigStructureKind.Localization),
                    keyField: "Key",
                    sourcePath: "ConfigSource/Localization/{locale}.tsv");
                if (localizationKeys != null)
                {
                    foreach (LocalizedTextKey key in localizationKeys)
                        localizationSource.AddKey(key);
                }

                Register(sourceIndex, result.Report, localizationSource);

                return sourceIndex;
            }

            private static void Register(
                ConfigSourceIndex sourceIndex,
                AIActionDryRunReport report,
                ConfigSourceEntry source)
            {
                sourceIndex.Register(source);
                report.SourceRegistrations.Add(new AIActionDryRunSourceRegistration(
                    source.SourceId,
                    source.SourcePath,
                    source.KeyCount));
            }

            private static ConfigTable<AIActionIndexDryRunRow> CreateIndexTable()
            {
                var schema = new ConfigSchema<AIActionIndexDryRunRow>(
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
                    .AddField(new ConfigField(
                        "NameKey",
                        ConfigFieldType.LocalizedText,
                        required: true,
                        referenceRule: new ConfigReferenceRule(
                            "NameKey",
                            "Localization",
                            ConfigStructureKind.Localization,
                            "Key",
                            severity: ConfigValidationSeverity.Warning)))
                    .AddField(new ConfigField(
                        "DescKey",
                        ConfigFieldType.LocalizedText,
                        required: true,
                        referenceRule: new ConfigReferenceRule(
                            "DescKey",
                            "Localization",
                            ConfigStructureKind.Localization,
                            "Key",
                            severity: ConfigValidationSeverity.Warning)))
                    .AddField(new ConfigField("CanGet", ConfigFieldType.Boolean, required: true));

                return new ConfigTable<AIActionIndexDryRunRow>(schema);
            }

            private static ConfigTable<AIActionGraphDryRunModel> CreateGraphTable()
            {
                var schema = new ConfigSchema<AIActionGraphDryRunModel>(
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
                    .AddField(new ConfigField("CooldownMs", ConfigFieldType.Integer, required: true));

                return new ConfigTable<AIActionGraphDryRunModel>(schema);
            }

            private static void AddValidationIssues(
                AIActionDryRunReport targetReport,
                ConfigTableValidationReport sourceReport)
            {
                for (int i = 0; i < sourceReport.Issues.Count; i++)
                {
                    ConfigTableValidationIssue issue = sourceReport.Issues[i];
                    targetReport.ValidationEntries.Add(new AIActionDryRunReportEntry(
                        issue.Severity,
                        GetRuleId(issue),
                        issue.Error,
                        issue.TableName,
                        issue.RowId,
                        issue.FieldName,
                        GetTargetSourceId(issue),
                        issue.Message));
                }
            }

            private static void AddEnumDomainWarnings(
                AIActionDryRunReport report,
                IReadOnlyList<AIActionGraphDryRunModel> graphs)
            {
                for (int i = 0; i < graphs.Count; i++)
                {
                    AIActionGraphDryRunModel graph = graphs[i];
                    for (int j = 0; j < graph.Conditions.Count; j++)
                    {
                        AIActionDryRunCondition condition = graph.Conditions[j];
                        if (!KnownConditionKeys.Contains(condition.Key))
                            AddEnumWarning(report, graph.Id, "Conditions[].Key", condition.Key, "GOAPWorldKey");
                        if (!KnownConditionCompares.Contains(condition.Compare))
                            AddEnumWarning(report, graph.Id, "Conditions[].Compare", condition.Compare, "GOAPCompare");
                    }

                    for (int j = 0; j < graph.Effects.Count; j++)
                    {
                        AIActionDryRunEffect effect = graph.Effects[j];
                        if (!KnownEffectTypes.Contains(effect.Effect))
                            AddEnumWarning(report, graph.Id, "Effects[].Effect", effect.Effect, "GOAPEffectType");
                    }
                }
            }

            private static void AddEnumWarning(
                AIActionDryRunReport report,
                int rowId,
                string fieldName,
                string value,
                string targetDomain)
            {
                report.ValidationEntries.Add(new AIActionDryRunReportEntry(
                    ConfigValidationSeverity.Warning,
                    "pilot02.aiaction.enum-domain",
                    ConfigError.NotFound,
                    "AIActionGraph",
                    rowId,
                    fieldName,
                    targetDomain,
                    "Unknown enum-domain value in dry-run evidence: " + value + "."));
            }

            private static string GetRuleId(ConfigTableValidationIssue issue)
            {
                if (issue.TableName == "AIActionIndex" && issue.FieldName == "Id")
                    return "pilot02.aiaction.index.graph";

                if (issue.TableName == "AIActionGraph" && issue.FieldName == "AbilityId")
                    return "pilot02.aiaction.ability";

                if (issue.TableName == "AIActionIndex" &&
                    (issue.FieldName == "NameKey" || issue.FieldName == "DescKey"))
                {
                    return "pilot02.aiaction.localization";
                }

                return "pilot02.aiaction.unknown";
            }

            private static string GetTargetSourceId(ConfigTableValidationIssue issue)
            {
                string ruleId = GetRuleId(issue);
                if (ruleId == "pilot02.aiaction.index.graph")
                    return "Graph:AIActionGraph";
                if (ruleId == "pilot02.aiaction.ability")
                    return "Graph:AbilityGraph";
                if (ruleId == "pilot02.aiaction.localization")
                    return "Localization:Localization";

                return string.Empty;
            }
        }

        private sealed class AIActionDryRunSource
        {
            public AIActionDryRunSource()
            {
                IndexFields = new Dictionary<string, object>(StringComparer.Ordinal);
                GraphFields = new Dictionary<string, object>(StringComparer.Ordinal);
                AbilityGraphKeys = new List<AbilityGraphKeyEvidence>();
            }

            public Dictionary<string, object> IndexFields { get; }
            public Dictionary<string, object> GraphFields { get; }
            public List<AbilityGraphKeyEvidence> AbilityGraphKeys { get; }
        }

        private sealed class AIActionDryRunResult
        {
            public AIActionDryRunResult(
                IReadOnlyList<AIActionIndexDryRunRow> indexRows,
                IReadOnlyList<AIActionGraphDryRunModel> graphs,
                IReadOnlyList<AbilityGraphKeyEvidence> abilityGraphKeys,
                AIActionDryRunReport report)
            {
                IndexRows = indexRows;
                Graphs = graphs;
                AbilityGraphKeys = abilityGraphKeys;
                Report = report;
            }

            public IReadOnlyList<AIActionIndexDryRunRow> IndexRows { get; }
            public IReadOnlyList<AIActionGraphDryRunModel> Graphs { get; }
            public IReadOnlyList<AbilityGraphKeyEvidence> AbilityGraphKeys { get; }
            public AIActionDryRunReport Report { get; }
        }

        private readonly struct AIActionIndexDryRunRow : IConfigData
        {
            public AIActionIndexDryRunRow(int id, LocalizedTextKey nameKey, LocalizedTextKey descKey, bool canGet)
            {
                Id = id;
                NameKey = nameKey;
                DescKey = descKey;
                CanGet = canGet;
            }

            public int Id { get; }
            public LocalizedTextKey NameKey { get; }
            public LocalizedTextKey DescKey { get; }
            public bool CanGet { get; }
        }

        private sealed class AIActionGraphDryRunModel : IConfigData
        {
            public AIActionGraphDryRunModel(
                int id,
                string name,
                int abilityId,
                int cost,
                int cooldownMs,
                IReadOnlyList<AIActionDryRunCondition> conditions,
                IReadOnlyList<AIActionDryRunEffect> effects)
            {
                Id = id;
                Name = name ?? string.Empty;
                AbilityId = abilityId;
                Cost = cost;
                CooldownMs = cooldownMs;
                Conditions = conditions ?? Array.Empty<AIActionDryRunCondition>();
                Effects = effects ?? Array.Empty<AIActionDryRunEffect>();
            }

            public int Id { get; }
            public string Name { get; }
            public int AbilityId { get; }
            public int Cost { get; }
            public int CooldownMs { get; }
            public IReadOnlyList<AIActionDryRunCondition> Conditions { get; }
            public IReadOnlyList<AIActionDryRunEffect> Effects { get; }
        }

        private readonly struct AIActionDryRunCondition
        {
            public AIActionDryRunCondition(string key, string compare, string value)
            {
                Key = key ?? string.Empty;
                Compare = compare ?? string.Empty;
                Value = value ?? string.Empty;
            }

            public string Key { get; }
            public string Compare { get; }
            public string Value { get; }
        }

        private readonly struct AIActionDryRunEffect
        {
            public AIActionDryRunEffect(string key, string effect)
            {
                Key = key ?? string.Empty;
                Effect = effect ?? string.Empty;
            }

            public string Key { get; }
            public string Effect { get; }
        }

        private readonly struct AbilityGraphKeyEvidence
        {
            public AbilityGraphKeyEvidence(int id, string evidencePath)
            {
                Id = id;
                EvidencePath = evidencePath ?? string.Empty;
            }

            public int Id { get; }
            public string EvidencePath { get; }
        }

        private sealed class AIActionDryRunReport
        {
            public AIActionDryRunReport()
            {
                UnsupportedFields = new List<AIActionUnsupportedFieldReportEntry>();
                SentinelCandidates = new List<AIActionSentinelCandidateReportEntry>();
                SourceRegistrations = new List<AIActionDryRunSourceRegistration>();
                ValidationEntries = new List<AIActionDryRunReportEntry>();
                GeneratedSourceFiles = new List<string>();
            }

            public List<AIActionUnsupportedFieldReportEntry> UnsupportedFields { get; }
            public List<AIActionSentinelCandidateReportEntry> SentinelCandidates { get; }
            public List<AIActionDryRunSourceRegistration> SourceRegistrations { get; }
            public List<AIActionDryRunReportEntry> ValidationEntries { get; }
            public List<string> GeneratedSourceFiles { get; }
            public bool RuntimeBytesGenerated => false;
            public int ErrorCount => Count(ConfigValidationSeverity.Error);
            public int WarningCount => Count(ConfigValidationSeverity.Warning);
            public int InfoCount => Count(ConfigValidationSeverity.Info);
            public bool HasErrors => ErrorCount > 0;
            public bool HasSentinelCandidates => SentinelCandidates.Count > 0;

            private int Count(ConfigValidationSeverity severity)
            {
                int count = 0;
                for (int i = 0; i < ValidationEntries.Count; i++)
                {
                    if (ValidationEntries[i].Severity == severity)
                        count++;
                }

                return count;
            }
        }

        private readonly struct AIActionDryRunSourceRegistration
        {
            public AIActionDryRunSourceRegistration(string sourceId, string sourcePath, int keyCount)
            {
                SourceId = sourceId ?? string.Empty;
                SourcePath = sourcePath ?? string.Empty;
                KeyCount = keyCount;
            }

            public string SourceId { get; }
            public string SourcePath { get; }
            public int KeyCount { get; }
        }

        private readonly struct AIActionDryRunReportEntry
        {
            public AIActionDryRunReportEntry(
                ConfigValidationSeverity severity,
                string ruleId,
                ConfigError error,
                string sourceSection,
                int rowId,
                string fieldName,
                string targetSourceId,
                string message)
            {
                Severity = severity;
                RuleId = ruleId ?? string.Empty;
                Error = error;
                SourceSection = sourceSection ?? string.Empty;
                RowId = rowId;
                FieldName = fieldName ?? string.Empty;
                TargetSourceId = targetSourceId ?? string.Empty;
                Message = message ?? string.Empty;
            }

            public ConfigValidationSeverity Severity { get; }
            public string RuleId { get; }
            public ConfigError Error { get; }
            public string SourceSection { get; }
            public int RowId { get; }
            public string FieldName { get; }
            public string TargetSourceId { get; }
            public string Message { get; }
        }

        private readonly struct AIActionUnsupportedFieldReportEntry
        {
            public AIActionUnsupportedFieldReportEntry(
                string sourceSection,
                string sourceField,
                string destinationShape,
                string reason)
            {
                SourceSection = sourceSection ?? string.Empty;
                SourceField = sourceField ?? string.Empty;
                DestinationShape = destinationShape ?? string.Empty;
                Reason = reason ?? string.Empty;
            }

            public string SourceSection { get; }
            public string SourceField { get; }
            public string DestinationShape { get; }
            public string Reason { get; }
        }

        private readonly struct AIActionSentinelCandidateReportEntry
        {
            public AIActionSentinelCandidateReportEntry(
                string sourceSection,
                string fieldName,
                string rawValue,
                string reason)
            {
                SourceSection = sourceSection ?? string.Empty;
                FieldName = fieldName ?? string.Empty;
                RawValue = rawValue ?? string.Empty;
                Reason = reason ?? string.Empty;
            }

            public string SourceSection { get; }
            public string FieldName { get; }
            public string RawValue { get; }
            public string Reason { get; }
        }
    }
}
