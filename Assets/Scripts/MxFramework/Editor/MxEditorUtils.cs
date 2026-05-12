using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Config;
using UnityEditor;

namespace MxFramework.Editor
{
    public static class MxEditorUtils
    {
        private const string ProjectRoot = ".";
        private const string ConfigReportExportDirectory = "Temp/MxFrameworkReports/Config";
        private const string ConfigHealthBaselineKey = "MxFramework.Editor.ConfigHealthBaseline.v1";
        private static readonly List<IConfigEditorSource> ExternalConfigSources = new List<IConfigEditorSource>();

        public static IReadOnlyList<FrameworkModuleInfo> GetModules()
        {
            return new[]
            {
                new FrameworkModuleInfo("Core", "MxFramework.Core", "Assets/Scripts/MxFramework/Core/MxFramework.Core.asmdef", "已就绪", "-"),
                new FrameworkModuleInfo("Core.Unity", "MxFramework.Core.Unity", "Assets/Scripts/MxFramework/Core.Unity/MxFramework.Core.Unity.asmdef", "已就绪", "Core"),
                new FrameworkModuleInfo("Events", "MxFramework.Events", "Assets/Scripts/MxFramework/Events/MxFramework.Events.asmdef", "已就绪", "Core"),
                new FrameworkModuleInfo("Attributes", "MxFramework.Attributes", "Assets/Scripts/MxFramework/Attributes/MxFramework.Attributes.asmdef", "已就绪", "Core, Events"),
                new FrameworkModuleInfo("Buffs", "MxFramework.Buffs", "Assets/Scripts/MxFramework/Buffs/MxFramework.Buffs.asmdef", "已就绪", "Core, Events, Attributes"),
                new FrameworkModuleInfo("Modifiers", "MxFramework.Modifiers", "Assets/Scripts/MxFramework/Modifiers/MxFramework.Modifiers.asmdef", "已就绪", "Core, Events, Attributes, Buffs"),
                new FrameworkModuleInfo("Config", "MxFramework.Config", "Assets/Scripts/MxFramework/Config/MxFramework.Config.asmdef", "已就绪", "Core"),
                new FrameworkModuleInfo("Config.Runtime", "MxFramework.Config.Runtime", "Assets/Scripts/MxFramework/Config.Runtime/MxFramework.Config.Runtime.asmdef", "已就绪", "Core, Config, Buffs, Modifiers"),
                new FrameworkModuleInfo("AI", "MxFramework.AI", "Assets/Scripts/MxFramework/AI/MxFramework.AI.asmdef", "已就绪", "Core"),
                new FrameworkModuleInfo("Diagnostics", "MxFramework.Diagnostics", "Assets/Scripts/MxFramework/Diagnostics/MxFramework.Diagnostics.asmdef", "已就绪", "Core"),
                new FrameworkModuleInfo("Resources", "MxFramework.Resources", "Assets/Scripts/MxFramework/Resources/MxFramework.Resources.asmdef", "已就绪", "Core, Diagnostics"),
                new FrameworkModuleInfo("Resources.Unity", "MxFramework.Resources.Unity", "Assets/Scripts/MxFramework/Resources.Unity/MxFramework.Resources.Unity.asmdef", "已就绪", "Resources")
            };
        }

        public static bool ValidateModuleAssets(IReadOnlyList<FrameworkModuleInfo> modules, out string report)
        {
            var lines = new List<string>();
            bool valid = true;
            for (int i = 0; i < modules.Count; i++)
            {
                FrameworkModuleInfo module = modules[i];
                if (File.Exists(module.AssetPath))
                {
                    lines.Add("[正常] " + module.AssemblyName + " -> " + module.AssetPath);
                    continue;
                }

                valid = false;
                lines.Add("[缺失] " + module.AssemblyName + " -> " + module.AssetPath);
            }

            report = string.Join("\n", lines);
            return valid;
        }

        public static IReadOnlyList<ConfigSchema> GetBuiltInConfigSchemas()
        {
            return ConfigDemoSources.CreateSchemas();
        }

        public static IReadOnlyList<IConfigEditorSource> GetBuiltInConfigSources()
        {
            return ConfigDemoSources.CreateSources();
        }

        public static IReadOnlyList<IConfigEditorSource> GetConfigEditorSources()
        {
            var sources = new List<IConfigEditorSource>(GetBuiltInConfigSources());
            sources.AddRange(ExternalConfigSources);
            return sources;
        }

        public static void RegisterConfigEditorSource(IConfigEditorSource source)
        {
            if (source == null)
                return;

            for (int i = 0; i < ExternalConfigSources.Count; i++)
            {
                if (ExternalConfigSources[i].Name == source.Name)
                {
                    ExternalConfigSources[i] = source;
                    return;
                }
            }

            ExternalConfigSources.Add(source);
        }

        public static void ClearConfigEditorSources()
        {
            ExternalConfigSources.Clear();
        }

        public static string CreateConfigSchemaReport(ConfigSchema schema)
        {
            if (schema == null)
                return string.Empty;

            ConfigAuthoringTemplate template = ConfigAuthoring.CreateTemplate(schema);
            var builder = new StringBuilder();
            builder.Append("MxFramework Config Schema\n");
            builder.Append("table: ").Append(schema.TableName).Append('\n');
            builder.Append("displayName: ").Append(schema.DisplayName).Append('\n');
            builder.Append("description: ").Append(schema.Description).Append('\n');
            if (schema.IdRange.IsValid)
                builder.Append("idRange: ").Append(schema.IdRange.MinInclusive).Append('-').Append(schema.IdRange.MaxInclusive).Append('\n');
            builder.Append('\n');
            builder.Append(template.Text);
            return builder.ToString();
        }

        public static string CreateConfigSourceReport(IConfigEditorSource source)
        {
            return source == null ? string.Empty : source.CreateReport();
        }

        public static ConfigHealthReport AnalyzeConfigHealth(IReadOnlyList<IConfigEditorSource> sources)
        {
            var health = new ConfigHealthReport();
            if (sources == null)
                return health;

            ConfigSourceIndex sourceIndex = BuildConfigSourceIndex(sources, health);
            for (int i = 0; i < sources.Count; i++)
            {
                IConfigEditorSource source = sources[i];
                if (source == null)
                    continue;

                ConfigAuthoringReport report = ValidateConfigSource(source, sourceIndex);
                int errors = 0;
                int warnings = 0;
                for (int j = 0; j < report.Issues.Count; j++)
                {
                    ConfigAuthoringIssue issue = report.Issues[j];
                    if (issue.Severity == ConfigValidationSeverity.Error)
                        errors++;
                    else if (issue.Severity == ConfigValidationSeverity.Warning)
                        warnings++;

                    health.AddIssue(issue);
                }

                health.AddSource(new ConfigSourceHealth(
                    source.Name,
                    source.Schema.TableName,
                    source.SourceType,
                    source.RowCount,
                    errors,
                    warnings));
            }

            return health;
        }

        public static string CreateConfigHealthReport(ConfigHealthReport health)
        {
            if (health == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append("MxFramework Config Health Report\n");
            builder.Append("sources: ").Append(health.SourceCount).Append('\n');
            builder.Append("rows: ").Append(health.TotalRows).Append('\n');
            builder.Append("problemSources: ").Append(health.ProblemSourceCount).Append('\n');
            builder.Append("errors: ").Append(health.ErrorCount).Append('\n');
            builder.Append("warnings: ").Append(health.WarningCount).Append('\n');
            builder.Append("missingReferences: ").Append(health.MissingReferenceCount).Append('\n');
            builder.Append("missingLocalization: ").Append(health.MissingLocalizationCount).Append('\n');
            builder.Append("invalidIds: ").Append(health.InvalidIdCount).Append('\n');
            builder.Append("schemaIssues: ").Append(health.SchemaIssueCount).Append('\n');
            builder.Append("indexedSources: ").Append(health.SourceIndexCount).Append('\n');
            builder.Append("indexedKeys: ").Append(health.SourceKeyCount).Append('\n');

            builder.Append("\nsources:\n");
            for (int i = 0; i < health.Sources.Count; i++)
            {
                ConfigSourceHealth source = health.Sources[i];
                builder.Append("- ")
                    .Append(source.Status)
                    .Append(" source=").Append(source.SourceName)
                    .Append(", table=").Append(source.TableName)
                    .Append(", type=").Append(source.SourceType)
                    .Append(", rows=").Append(source.RowCount)
                    .Append(", errors=").Append(source.ErrorCount)
                    .Append(", warnings=").Append(source.WarningCount)
                    .Append('\n');
            }

            builder.Append("\nissueStats:\n");
            if (health.IssueStats.Count == 0)
            {
                builder.Append("- none\n");
            }
            else
            {
                for (int i = 0; i < health.IssueStats.Count; i++)
                {
                    ConfigIssueStat stat = health.IssueStats[i];
                    builder.Append("- ").Append(stat.Error).Append(": ").Append(stat.Count).Append('\n');
                }
            }

            builder.Append("\nsamples:\n");
            if (health.SampleIssues.Count == 0)
            {
                builder.Append("- none\n");
            }
            else
            {
                for (int i = 0; i < health.SampleIssues.Count; i++)
                {
                    ConfigAuthoringIssue issue = health.SampleIssues[i];
                    builder.Append("- ")
                        .Append(issue.TableName)
                        .Append(" row=").Append(issue.RowId)
                        .Append(" field=").Append(issue.FieldName)
                        .Append(" error=").Append(issue.Error)
                        .Append(": ").Append(issue.Message)
                        .Append('\n');
                }
            }

            return builder.ToString();
        }

        public static IReadOnlyList<ConfigIssueView> CollectConfigIssues(IReadOnlyList<IConfigEditorSource> sources)
        {
            var issues = new List<ConfigIssueView>();
            if (sources == null)
                return issues;

            ConfigSourceIndex sourceIndex = BuildConfigSourceIndex(sources, null);
            for (int i = 0; i < sources.Count; i++)
            {
                IConfigEditorSource source = sources[i];
                if (source == null)
                    continue;

                ConfigAuthoringReport report = ValidateConfigSource(source, sourceIndex);
                for (int j = 0; j < report.Issues.Count; j++)
                    issues.Add(new ConfigIssueView(source.Name, report.Issues[j], FindFieldDisplayName(source.Schema, report.Issues[j].FieldName)));
            }

            return issues;
        }

        public static string CreateConfigIssueListText(IReadOnlyList<ConfigIssueView> issues, ConfigValidationSeverity? severityFilter = null)
        {
            var builder = new StringBuilder();
            builder.Append("MxFramework Config Issues\n");
            if (issues == null || issues.Count == 0)
            {
                builder.Append("- none\n");
                return builder.ToString();
            }

            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                ConfigIssueView view = issues[i];
                if (severityFilter.HasValue && view.Issue.Severity != severityFilter.Value)
                    continue;

                builder.Append("- ").Append(view.ToLine()).Append('\n');
                count++;
            }

            if (count == 0)
                builder.Append("- none\n");

            return builder.ToString();
        }

        public static string CreateConfigAiFixContext(
            IConfigEditorSource source,
            ConfigHealthReport health,
            IReadOnlyList<ConfigIssueView> issues,
            int previewRows,
            string selectedFieldName = "")
        {
            if (source == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append("MxFramework Config AI Fix Context\n");
            builder.Append("source: ").Append(source.Name).Append('\n');
            builder.Append("sourceType: ").Append(source.SourceType).Append('\n');
            builder.Append("table: ").Append(source.Schema.TableName).Append('\n');
            builder.Append("rowCount: ").Append(source.RowCount).Append('\n');
            builder.Append("structure: ").Append(source.Schema.StructureKind).Append('\n');
            builder.Append('\n');
            builder.Append(CreateConfigHealthReport(health)).Append('\n');
            builder.Append("fieldAliases:\n");
            AppendFieldAliases(builder, source.Schema);
            builder.Append("selectedField:\n");
            AppendSelectedField(builder, source.Schema, selectedFieldName);
            builder.Append("schema:\n");
            builder.Append(source.CreateTemplate().Text).Append('\n');
            builder.Append("tsvPreview:\n");
            builder.Append(source.CreateTsvPreview(previewRows)).Append('\n');
            builder.Append("issues:\n");
            bool hasIssue = false;
            if (issues != null)
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    ConfigIssueView issue = issues[i];
                    if (issue.SourceName != source.Name)
                        continue;

                    builder.Append("- ").Append(issue.ToLine()).Append('\n');
                    hasIssue = true;
                }
            }

            if (!hasIssue)
                builder.Append("- none\n");

            return builder.ToString();
        }

        private static void AppendSelectedField(StringBuilder builder, ConfigSchema schema, string selectedFieldName)
        {
            if (schema == null || string.IsNullOrEmpty(selectedFieldName))
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                if (field.Name != selectedFieldName)
                    continue;

                builder.Append("- field=").Append(field.Name).Append('\n');
                builder.Append("  display=").Append(field.DisplayName).Append('\n');
                builder.Append("  type=").Append(field.FieldType).Append('\n');
                builder.Append("  required=").Append(field.Required ? "true" : "false").Append('\n');
                builder.Append("  enumId=").Append(field.EnumId).Append('\n');
                builder.Append("  reference=").Append(field.ReferenceRule.IsValid ? field.ReferenceRule.GetTargetDisplayName() : string.Empty).Append('\n');
                builder.Append("  description=").Append(field.Description).Append('\n');
                return;
            }

            builder.Append("- none\n");
        }

        private static void AppendFieldAliases(StringBuilder builder, ConfigSchema schema)
        {
            if (schema == null || schema.Fields.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                builder.Append("- field=").Append(field.Name);
                if (!string.IsNullOrEmpty(field.DisplayName))
                    builder.Append(" display=").Append(field.DisplayName);
                builder.Append('\n');
            }
        }

        private static string FindFieldDisplayName(ConfigSchema schema, string fieldName)
        {
            if (schema == null || string.IsNullOrEmpty(fieldName))
                return string.Empty;

            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                if (field.Name == fieldName)
                    return field.DisplayName;
            }

            return string.Empty;
        }

        public static string LoadConfigHealthBaseline()
        {
            return EditorPrefs.GetString(ConfigHealthBaselineKey, string.Empty);
        }

        public static void SaveConfigHealthBaseline(ConfigHealthReport health)
        {
            EditorPrefs.SetString(ConfigHealthBaselineKey, CreateConfigHealthSnapshotText(health));
        }

        public static ConfigChangeReport DetectConfigChanges(ConfigHealthReport current, string baselineText)
        {
            if (current == null || string.IsNullOrEmpty(baselineText))
                return ConfigChangeReport.NoBaseline();

            ConfigHealthSnapshot previous = ParseConfigHealthSnapshot(baselineText);
            if (previous == null)
                return ConfigChangeReport.NoBaseline();

            ConfigHealthSnapshot latest = CreateConfigHealthSnapshot(current);
            ConfigChangeReport report = ConfigChangeReport.WithBaseline();
            AddSourceChanges(report, previous, latest);
            AddIssueStatChanges(report, previous, latest);
            return report;
        }

        public static string CreateConfigHealthSnapshotText(ConfigHealthReport health)
        {
            ConfigHealthSnapshot snapshot = CreateConfigHealthSnapshot(health);
            var builder = new StringBuilder();
            builder.Append("version\t1\n");
            for (int i = 0; i < snapshot.Sources.Count; i++)
            {
                ConfigSourceSnapshot source = snapshot.Sources[i];
                builder.Append("source\t")
                    .Append(SanitizeSnapshotValue(source.SourceName)).Append('\t')
                    .Append(SanitizeSnapshotValue(source.TableName)).Append('\t')
                    .Append(SanitizeSnapshotValue(source.SourceType)).Append('\t')
                    .Append(source.RowCount).Append('\t')
                    .Append(source.ErrorCount).Append('\t')
                    .Append(source.WarningCount).Append('\n');
            }

            for (int i = 0; i < snapshot.IssueStats.Count; i++)
            {
                ConfigIssueStatSnapshot stat = snapshot.IssueStats[i];
                builder.Append("issue\t").Append(stat.Error).Append('\t').Append(stat.Count).Append('\n');
            }

            return builder.ToString();
        }

        public static string CreateConfigChangeReportText(ConfigChangeReport report)
        {
            var builder = new StringBuilder();
            builder.Append("MxFramework Config Change Report\n");
            if (report == null || !report.HasBaseline)
            {
                builder.Append("baseline: none\n");
                builder.Append("changes: unknown\n");
                return builder.ToString();
            }

            builder.Append("baseline: available\n");
            builder.Append("changes: ").Append(report.HasChanges ? "yes" : "no").Append('\n');
            builder.Append("addedSources: ").Append(report.AddedSourceCount).Append('\n');
            builder.Append("removedSources: ").Append(report.RemovedSourceCount).Append('\n');
            builder.Append("changedSources: ").Append(report.ChangedSourceCount).Append('\n');
            builder.Append("issueTypeChanges: ").Append(report.IssueTypeChangeCount).Append('\n');

            builder.Append("\nsources:\n");
            if (report.SourceChanges.Count == 0)
            {
                builder.Append("- none\n");
            }
            else
            {
                for (int i = 0; i < report.SourceChanges.Count; i++)
                {
                    ConfigSourceChange change = report.SourceChanges[i];
                    builder.Append("- ")
                        .Append(change.ChangeType)
                        .Append(" source=").Append(change.SourceName)
                        .Append(", table=").Append(change.TableName)
                        .Append(", rows=").Append(change.PreviousRows).Append("->").Append(change.CurrentRows)
                        .Append(" (delta=").Append(FormatDelta(change.RowDelta)).Append(')')
                        .Append(", errors=").Append(change.PreviousErrors).Append("->").Append(change.CurrentErrors)
                        .Append(" (delta=").Append(FormatDelta(change.ErrorDelta)).Append(')')
                        .Append(", warnings=").Append(change.PreviousWarnings).Append("->").Append(change.CurrentWarnings)
                        .Append(" (delta=").Append(FormatDelta(change.WarningDelta)).Append(')')
                        .Append('\n');
                }
            }

            builder.Append("\nissueStats:\n");
            if (report.IssueStatChanges.Count == 0)
            {
                builder.Append("- none\n");
            }
            else
            {
                for (int i = 0; i < report.IssueStatChanges.Count; i++)
                {
                    ConfigIssueStatChange change = report.IssueStatChanges[i];
                    builder.Append("- ")
                        .Append(change.Error)
                        .Append(": ")
                        .Append(change.PreviousCount)
                        .Append("->")
                        .Append(change.CurrentCount)
                        .Append(" (delta=")
                        .Append(FormatDelta(change.Delta))
                        .Append(")\n");
                }
            }

            return builder.ToString();
        }

        public static string CreateConfigPrecommitReportText(
            ConfigHealthReport health,
            ConfigChangeReport changes,
            IReadOnlyList<ConfigIssueView> issues)
        {
            var builder = new StringBuilder();
            bool hasErrors = health != null && health.ErrorCount > 0;
            bool hasWarnings = health != null && health.WarningCount > 0;
            builder.Append("MxFramework Config Precommit Report\n");
            builder.Append("result: ").Append(hasErrors ? "blocked" : hasWarnings ? "warning" : "ready").Append('\n');
            builder.Append("canCommit: ").Append(hasErrors ? "false" : "true").Append('\n');
            builder.Append("errors: ").Append(health != null ? health.ErrorCount : 0).Append('\n');
            builder.Append("warnings: ").Append(health != null ? health.WarningCount : 0).Append('\n');
            builder.Append("problemSources: ").Append(health != null ? health.ProblemSourceCount : 0).Append('\n');
            builder.Append("indexedSources: ").Append(health != null ? health.SourceIndexCount : 0).Append('\n');
            builder.Append("indexedKeys: ").Append(health != null ? health.SourceKeyCount : 0).Append('\n');
            builder.Append("changed: ").Append(changes != null && changes.HasChanges ? "true" : "false").Append('\n');
            builder.Append("addedSources: ").Append(changes != null ? changes.AddedSourceCount : 0).Append('\n');
            builder.Append("removedSources: ").Append(changes != null ? changes.RemovedSourceCount : 0).Append('\n');
            builder.Append("changedSources: ").Append(changes != null ? changes.ChangedSourceCount : 0).Append('\n');
            builder.Append('\n');
            builder.Append("decision:\n");
            if (hasErrors)
                builder.Append("- 不可提交：配置存在 Error，必须先修复。\n");
            else if (hasWarnings)
                builder.Append("- 可提交但需确认：配置存在 Warning。\n");
            else
                builder.Append("- 可提交：未发现 Error 或 Warning。\n");

            builder.Append('\n');
            builder.Append("requiredReports:\n");
            builder.Append("- config_health.txt\n");
            builder.Append("- config_issues.txt\n");
            builder.Append("- config_changes.txt\n");
            builder.Append("- config_ai_context.txt\n");
            builder.Append("- config_report_index.txt\n");

            builder.Append('\n');
            builder.Append("issues:\n");
            builder.Append(CreateConfigIssueListText(issues));
            return builder.ToString();
        }

        public static ConfigReportExportResult ExportConfigReportBundle(
            IConfigEditorSource source,
            ConfigHealthReport health,
            IReadOnlyList<ConfigIssueView> issues,
            ConfigChangeReport changes,
            int previewRows)
        {
            string directory = Path.GetFullPath(Path.Combine(ProjectRoot, ConfigReportExportDirectory));
            Directory.CreateDirectory(directory);

            var result = new ConfigReportExportResult(directory);
            WriteReportFile(result, "config_health.txt", CreateConfigHealthReport(health));
            WriteReportFile(result, "config_issues.txt", CreateConfigIssueListText(issues));
            WriteReportFile(result, "config_changes.txt", CreateConfigChangeReportText(changes));
            WriteReportFile(result, "config_ai_context.txt", CreateConfigAiFixContext(source, health, issues, previewRows));
            WriteReportFile(result, "config_precommit.txt", CreateConfigPrecommitReportText(health, changes, issues));
            WriteReportFile(result, "config_report_index.txt", CreateConfigReportIndexText(result));
            return result;
        }

        public static ConfigSourceIndex BuildConfigSourceIndex(IReadOnlyList<IConfigEditorSource> sources, ConfigHealthReport health = null)
        {
            var index = new ConfigSourceIndex();
            if (sources == null)
                return index;

            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] is IConfigEditorSourceIndexProvider provider)
                {
                    ConfigSourceEntry entry = provider.CreateSourceEntry();
                    if (index.Register(entry))
                        health?.AddSourceIndex(entry);
                }
            }

            return index;
        }

        private static ConfigAuthoringReport ValidateConfigSource(IConfigEditorSource source, ConfigSourceIndex sourceIndex)
        {
            if (source is IConfigEditorSourceIndexProvider provider)
                return provider.Validate(sourceIndex);

            return source.Validate();
        }

        public static bool ValidateBuiltInConfigSamples(out string report)
        {
            IReadOnlyList<IConfigEditorSource> sources = GetBuiltInConfigSources();
            ConfigSourceIndex sourceIndex = BuildConfigSourceIndex(sources);
            var builder = new StringBuilder();
            builder.Append("内置配置 Demo 校验\n");
            builder.Append("说明：当前校验的是框架内置纯净 Demo，用于验证 Schema、引用、多语言、enum、flags 和 SourceIndex 规则。\n\n");
            bool valid = true;
            for (int i = 0; i < sources.Count; i++)
            {
                ConfigAuthoringReport sourceReport = ValidateConfigSource(sources[i], sourceIndex);
                if (sourceReport.HasErrors)
                    valid = false;

                AppendConfigAuthoringReport(builder, sources[i].Name, sourceReport);
                if (i < sources.Count - 1)
                    builder.Append('\n');
            }

            report = builder.ToString();
            return valid;
        }

        public static void OpenProjectDocument(string relativePath)
        {
            string path = Path.GetFullPath(Path.Combine(ProjectRoot, relativePath));
            if (File.Exists(path))
            {
                EditorUtility.OpenWithDefaultApp(path);
            }
            else
            {
                EditorUtility.DisplayDialog("MxFramework", "文档不存在：\n" + path, "确定");
            }
        }

        public static string CreateFrameworkManagerReport(string mode, string validationReport)
        {
            var builder = new StringBuilder();
            builder.Append("MxFramework Framework Manager Report\n");
            builder.Append("mode: ").Append(mode ?? string.Empty).Append('\n');
            builder.Append("validation:\n");
            builder.Append(validationReport ?? string.Empty).Append('\n');
            return builder.ToString();
        }

        private static void AppendConfigAuthoringReport(StringBuilder builder, string tableName, ConfigAuthoringReport report)
        {
            builder.Append(tableName).Append(report.HasErrors ? "：发现错误" : "：正常").Append('\n');
            if (report.Issues.Count == 0)
            {
                builder.Append("- 没有发现问题。\n");
                return;
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                ConfigAuthoringIssue issue = report.Issues[i];
                builder.Append("- ")
                    .Append(issue.Severity)
                    .Append(" row=")
                    .Append(issue.RowId)
                    .Append(" field=")
                    .Append(issue.FieldName)
                    .Append(" error=")
                    .Append(issue.Error)
                    .Append("：")
                    .Append(issue.Message)
                    .Append('\n');
            }
        }

        private static void WriteReportFile(ConfigReportExportResult result, string fileName, string text)
        {
            string path = Path.Combine(result.Directory, fileName);
            File.WriteAllText(path, text ?? string.Empty, Encoding.UTF8);
            result.AddFile(path);
        }

        private static string CreateConfigReportIndexText(ConfigReportExportResult result)
        {
            var builder = new StringBuilder();
            builder.Append("MxFramework Config Report Bundle\n");
            builder.Append("directory: ").Append(result.Directory).Append('\n');
            builder.Append("files:\n");
            for (int i = 0; i < result.Files.Count; i++)
                builder.Append("- ").Append(Path.GetFileName(result.Files[i])).Append('\n');
            return builder.ToString();
        }

        private static ConfigHealthSnapshot CreateConfigHealthSnapshot(ConfigHealthReport health)
        {
            var snapshot = new ConfigHealthSnapshot();
            if (health == null)
                return snapshot;

            for (int i = 0; i < health.Sources.Count; i++)
            {
                ConfigSourceHealth source = health.Sources[i];
                snapshot.Sources.Add(new ConfigSourceSnapshot(
                    source.SourceName,
                    source.TableName,
                    source.SourceType,
                    source.RowCount,
                    source.ErrorCount,
                    source.WarningCount));
            }

            for (int i = 0; i < health.IssueStats.Count; i++)
            {
                ConfigIssueStat stat = health.IssueStats[i];
                snapshot.IssueStats.Add(new ConfigIssueStatSnapshot(stat.Error, stat.Count));
            }

            return snapshot;
        }

        private static ConfigHealthSnapshot ParseConfigHealthSnapshot(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            var snapshot = new ConfigHealthSnapshot();
            string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('\t');
                if (parts.Length == 0)
                    continue;

                if (parts[0] == "source")
                {
                    if (parts.Length < 7)
                        return null;

                    if (!int.TryParse(parts[4], out int rows) ||
                        !int.TryParse(parts[5], out int errors) ||
                        !int.TryParse(parts[6], out int warnings))
                        return null;

                    snapshot.Sources.Add(new ConfigSourceSnapshot(parts[1], parts[2], parts[3], rows, errors, warnings));
                }
                else if (parts[0] == "issue")
                {
                    if (parts.Length < 3)
                        return null;

                    if (!Enum.TryParse(parts[1], out ConfigError error) || !int.TryParse(parts[2], out int count))
                        return null;

                    snapshot.IssueStats.Add(new ConfigIssueStatSnapshot(error, count));
                }
            }

            return snapshot;
        }

        private static void AddSourceChanges(ConfigChangeReport report, ConfigHealthSnapshot previous, ConfigHealthSnapshot latest)
        {
            for (int i = 0; i < latest.Sources.Count; i++)
            {
                ConfigSourceSnapshot current = latest.Sources[i];
                ConfigSourceSnapshot old = FindSource(previous, current.SourceName);
                if (old == null)
                {
                    report.AddSourceChange(new ConfigSourceChange(
                        ConfigSourceChangeType.Added,
                        current.SourceName,
                        current.TableName,
                        0,
                        current.RowCount,
                        0,
                        current.ErrorCount,
                        0,
                        current.WarningCount));
                    continue;
                }

                if (old.RowCount != current.RowCount || old.ErrorCount != current.ErrorCount || old.WarningCount != current.WarningCount || old.TableName != current.TableName)
                {
                    report.AddSourceChange(new ConfigSourceChange(
                        ConfigSourceChangeType.Changed,
                        current.SourceName,
                        current.TableName,
                        old.RowCount,
                        current.RowCount,
                        old.ErrorCount,
                        current.ErrorCount,
                        old.WarningCount,
                        current.WarningCount));
                }
            }

            for (int i = 0; i < previous.Sources.Count; i++)
            {
                ConfigSourceSnapshot old = previous.Sources[i];
                if (FindSource(latest, old.SourceName) != null)
                    continue;

                report.AddSourceChange(new ConfigSourceChange(
                    ConfigSourceChangeType.Removed,
                    old.SourceName,
                    old.TableName,
                    old.RowCount,
                    0,
                    old.ErrorCount,
                    0,
                    old.WarningCount,
                    0));
            }
        }

        private static void AddIssueStatChanges(ConfigChangeReport report, ConfigHealthSnapshot previous, ConfigHealthSnapshot latest)
        {
            for (int i = 0; i < latest.IssueStats.Count; i++)
            {
                ConfigIssueStatSnapshot current = latest.IssueStats[i];
                ConfigIssueStatSnapshot old = FindIssueStat(previous, current.Error);
                int oldCount = old != null ? old.Count : 0;
                if (oldCount != current.Count)
                    report.AddIssueStatChange(new ConfigIssueStatChange(current.Error, oldCount, current.Count));
            }

            for (int i = 0; i < previous.IssueStats.Count; i++)
            {
                ConfigIssueStatSnapshot old = previous.IssueStats[i];
                if (FindIssueStat(latest, old.Error) == null)
                    report.AddIssueStatChange(new ConfigIssueStatChange(old.Error, old.Count, 0));
            }
        }

        private static ConfigSourceSnapshot FindSource(ConfigHealthSnapshot snapshot, string sourceName)
        {
            for (int i = 0; i < snapshot.Sources.Count; i++)
            {
                if (snapshot.Sources[i].SourceName == sourceName)
                    return snapshot.Sources[i];
            }

            return null;
        }

        private static ConfigIssueStatSnapshot FindIssueStat(ConfigHealthSnapshot snapshot, ConfigError error)
        {
            for (int i = 0; i < snapshot.IssueStats.Count; i++)
            {
                if (snapshot.IssueStats[i].Error == error)
                    return snapshot.IssueStats[i];
            }

            return null;
        }

        private static string FormatDelta(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        private static string SanitizeSnapshotValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        }

        private sealed class ConfigHealthSnapshot
        {
            public readonly List<ConfigSourceSnapshot> Sources = new List<ConfigSourceSnapshot>();
            public readonly List<ConfigIssueStatSnapshot> IssueStats = new List<ConfigIssueStatSnapshot>();
        }

        private sealed class ConfigSourceSnapshot
        {
            public ConfigSourceSnapshot(string sourceName, string tableName, string sourceType, int rowCount, int errorCount, int warningCount)
            {
                SourceName = sourceName ?? string.Empty;
                TableName = tableName ?? string.Empty;
                SourceType = sourceType ?? string.Empty;
                RowCount = rowCount;
                ErrorCount = errorCount;
                WarningCount = warningCount;
            }

            public string SourceName { get; }
            public string TableName { get; }
            public string SourceType { get; }
            public int RowCount { get; }
            public int ErrorCount { get; }
            public int WarningCount { get; }
        }

        private sealed class ConfigIssueStatSnapshot
        {
            public ConfigIssueStatSnapshot(ConfigError error, int count)
            {
                Error = error;
                Count = count;
            }

            public ConfigError Error { get; }
            public int Count { get; }
        }
    }
}
