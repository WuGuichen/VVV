using System.Collections.Generic;
using MxFramework.Config;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed partial class FrameworkManager
    {
        private void RefreshConfigSourceListInPlace()
        {
            string selected = _selectedConfigSourceName;
            _configSources = MxEditorUtils.GetConfigEditorSources();
            if (_configSources.Count == 0)
                return;

            _selectedConfigSourceName = ContainsConfigSource(selected) ? selected : _configSources[0].Name;
        }

        private void EnsureConfigSources()
        {
            if (_configSources == null)
                _configSources = MxEditorUtils.GetConfigEditorSources();

            if (_configSources.Count > 0 && !ContainsConfigSource(_selectedConfigSourceName))
                _selectedConfigSourceName = _configSources[0].Name;
        }

        private IConfigEditorSource GetSelectedConfigSource()
        {
            EnsureConfigSources();
            if (_configSources.Count == 0)
                return null;

            for (int i = 0; i < _configSources.Count; i++)
            {
                if (_configSources[i].Name == _selectedConfigSourceName)
                    return _configSources[i];
            }

            return _configSources[0];
        }

        private bool ContainsConfigSource(string sourceName)
        {
            if (string.IsNullOrEmpty(sourceName) || _configSources == null)
                return false;

            for (int i = 0; i < _configSources.Count; i++)
            {
                if (_configSources[i].Name == sourceName)
                    return true;
            }

            return false;
        }

        private static string JoinLocales(IReadOnlyList<LocaleId> locales)
        {
            if (locales == null || locales.Count == 0)
                return string.Empty;

            string text = locales[0].ToString();
            for (int i = 1; i < locales.Count; i++)
                text += ", " + locales[i];
            return text;
        }

        private void RefreshConfigIssuesView()
        {
            if (_configIssuePreview == null)
                return;

            _configIssuePreview.value = MxEditorUtils.CreateConfigIssueListText(_configIssues, GetIssueSeverityFilter());
        }

        private ConfigValidationSeverity? GetIssueSeverityFilter()
        {
            string value = _configIssueFilterPopup != null ? _configIssueFilterPopup.value : "全部";
            if (value == "Error")
                return ConfigValidationSeverity.Error;
            if (value == "Warning")
                return ConfigValidationSeverity.Warning;
            return null;
        }

        private static string CreateHealthSummaryText(ConfigHealthReport health)
        {
            if (health == null)
                return "健康状态：未检测";

            return "健康状态：" + (health.HasErrors ? "错误" : health.HasWarnings ? "警告" : "正常") +
                "\n阶段：Config Workbench v0" +
                "\n配置源：" + health.SourceCount +
                "\n总行数：" + health.TotalRows +
                "\n问题源：" + health.ProblemSourceCount +
                "\nError：" + health.ErrorCount +
                "\nWarning：" + health.WarningCount +
                "\n缺失引用：" + health.MissingReferenceCount +
                "\n多语言缺失：" + health.MissingLocalizationCount +
                "\nID 问题：" + health.InvalidIdCount +
                "\nSchema 问题：" + health.SchemaIssueCount +
                "\n索引源：" + health.SourceIndexCount +
                "\n索引 Key：" + health.SourceKeyCount;
        }

        private static string CreateReferenceSummary(ConfigSchema schema)
        {
            if (schema == null || schema.ReferenceRules.Count == 0)
                return "- 无";

            string text = string.Empty;
            for (int i = 0; i < schema.ReferenceRules.Count; i++)
            {
                ConfigReferenceRule rule = schema.ReferenceRules[i];
                if (i > 0)
                    text += "\n";
                text += "- " + CreateReferenceFieldLabel(schema, rule.FieldName) + " -> " + rule.GetTargetDisplayName();
                if (!rule.Required)
                    text += "（可选）";
            }

            return text;
        }
    }
}
