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
        private void RefreshConfigSources()
        {
            _configSources = null;
            _configHealth = null;
            _configIssues = null;
            _configChangeReport = null;
            RebuildContent();
        }

        private void RefreshConfigHealth()
        {
            EnsureConfigSources();
            string baselineText = MxEditorUtils.LoadConfigHealthBaseline();
            _configHealth = MxEditorUtils.AnalyzeConfigHealth(_configSources);
            _configIssues = MxEditorUtils.CollectConfigIssues(_configSources);
            _configChangeReport = MxEditorUtils.DetectConfigChanges(_configHealth, baselineText);
            MxEditorUtils.SaveConfigHealthBaseline(_configHealth);
            if (_configHealthLabel != null)
            {
                _configHealthLabel.text = CreateHealthSummaryText(_configHealth);
                _configHealthLabel.style.color = _configHealth.HasErrors
                    ? new Color(1f, 0.55f, 0.35f)
                    : _configHealth.HasWarnings
                        ? new Color(0.95f, 0.78f, 0.35f)
                        : new Color(0.45f, 0.78f, 0.45f);
            }

            if (_configChangeLabel != null)
            {
                _configChangeLabel.text = CreateConfigChangeSummaryText(_configChangeReport);
                _configChangeLabel.style.color = _configChangeReport != null && _configChangeReport.HasChanges
                    ? new Color(0.95f, 0.78f, 0.35f)
                    : new Color(0.55f, 0.55f, 0.55f);
            }

            RefreshConfigIssuesView();
        }

        private void CopyConfigTemplate()
        {
            IConfigEditorSource source = GetSelectedConfigSource();
            if (source == null)
                return;

            ConfigAuthoringTemplate template = source.CreateTemplate();
            EditorGUIUtility.systemCopyBuffer = template.Text;
            _lastReport = MxEditorUtils.CreateConfigSourceReport(source);
            ShowNotification(new GUIContent("TSV 模板已复制"));
        }

        private void CopyConfigHealthReport()
        {
            if (_configHealth == null)
                RefreshConfigHealth();

            _lastReport = MxEditorUtils.CreateConfigHealthReport(_configHealth);
            EditorGUIUtility.systemCopyBuffer = _lastReport ?? string.Empty;
            ShowNotification(new GUIContent("健康报告已复制"));
        }

        private void CopyConfigIssueList()
        {
            if (_configIssues == null)
                RefreshConfigHealth();

            _lastReport = MxEditorUtils.CreateConfigIssueListText(_configIssues, GetIssueSeverityFilter());
            EditorGUIUtility.systemCopyBuffer = _lastReport ?? string.Empty;
            ShowNotification(new GUIContent("问题列表已复制"));
        }

        private void CopyConfigAiFixContext()
        {
            IConfigEditorSource source = GetSelectedConfigSource();
            if (source == null)
                return;

            if (_configHealth == null || _configIssues == null)
                RefreshConfigHealth();

            _lastReport = MxEditorUtils.CreateConfigAiFixContext(source, _configHealth, _configIssues, 5, _selectedConfigFieldName);
            EditorGUIUtility.systemCopyBuffer = _lastReport ?? string.Empty;
            ShowNotification(new GUIContent("AI 修复上下文已复制"));
        }

        private void CopyConfigChangeReport()
        {
            if (_configChangeReport == null)
                RefreshConfigHealth();

            _lastReport = MxEditorUtils.CreateConfigChangeReportText(_configChangeReport);
            EditorGUIUtility.systemCopyBuffer = _lastReport ?? string.Empty;
            ShowNotification(new GUIContent("变动报告已复制"));
        }

        private void ResetConfigChangeBaseline()
        {
            if (_configHealth == null)
                RefreshConfigHealth();

            MxEditorUtils.SaveConfigHealthBaseline(_configHealth);
            _configChangeReport = ConfigChangeReport.WithBaseline();
            if (_configChangeLabel != null)
            {
                _configChangeLabel.text = CreateConfigChangeSummaryText(_configChangeReport);
                _configChangeLabel.style.color = new Color(0.55f, 0.55f, 0.55f);
            }

            _lastReport = MxEditorUtils.CreateConfigChangeReportText(_configChangeReport);
            ShowNotification(new GUIContent("变动基线已重置"));
        }

        private void ExportConfigReportBundle()
        {
            IConfigEditorSource source = GetSelectedConfigSource();
            if (source == null)
                return;

            if (_configHealth == null || _configIssues == null || _configChangeReport == null)
                RefreshConfigHealth();

            ConfigReportExportResult result = MxEditorUtils.ExportConfigReportBundle(source, _configHealth, _configIssues, _configChangeReport, 5);
            _lastReport = "配置报告已导出：\n" + result.Directory;
            EditorUtility.RevealInFinder(result.Directory);
            ShowNotification(new GUIContent("配置报告已导出"));
        }

        private void RunConfigPrecommitCheck()
        {
            RefreshConfigSourceListInPlace();
            RefreshConfigHealth();
            IConfigEditorSource source = GetSelectedConfigSource();
            if (source == null)
                return;

            ConfigReportExportResult result = MxEditorUtils.ExportConfigReportBundle(source, _configHealth, _configIssues, _configChangeReport, 5);
            _lastReport = MxEditorUtils.CreateConfigPrecommitReportText(_configHealth, _configChangeReport, _configIssues);
            EditorGUIUtility.systemCopyBuffer = _lastReport;

            bool hasErrors = _configHealth != null && _configHealth.ErrorCount > 0;
            bool hasWarnings = _configHealth != null && _configHealth.WarningCount > 0;
            string title = hasErrors ? "配置提交前检查：不可提交" : hasWarnings ? "配置提交前检查：有警告" : "配置提交前检查：可提交";
            string message = title + "\n\n报告目录：\n" + result.Directory + "\n\n结果已复制到剪贴板。";
            EditorUtility.DisplayDialog("MxFramework", message, "确定");
            ShowNotification(new GUIContent(hasErrors ? "不可提交" : hasWarnings ? "有警告" : "可提交"));
        }
    }
}
