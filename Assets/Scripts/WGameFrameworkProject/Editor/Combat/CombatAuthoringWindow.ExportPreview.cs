using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Combat.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Combat.Editor
{
    public sealed partial class CombatAuthoringWindow
    {
        private void CopyReport()
        {
            if (string.IsNullOrEmpty(_lastReportText))
            {
                RefreshValidation();
            }

            EditorGUIUtility.systemCopyBuffer = _lastReportText;
        }

        private void ExportJson()
        {
            ExportJsonPackage(true);
        }

        private void ExportJsonPackage(bool promptForFolder)
        {
            if (_actionSerialized != null)
            {
                _actionSerialized.ApplyModifiedProperties();
            }

            if (_bindingSerialized != null)
            {
                _bindingSerialized.ApplyModifiedProperties();
            }

            string sourceGuid = GetAssetGuid(_actionAsset);
            if (string.IsNullOrEmpty(sourceGuid))
            {
                sourceGuid = GetAssetGuid(_sceneBindingAsset);
            }

            _lastExportResult = CombatAuthoringJsonExporter.Export(
                _actionAsset,
                _sceneBindingAsset,
                BuildPackageId(_actionAsset),
                sourceGuid,
                CombatAuthoringJsonExporter.DefaultToolVersion);
            _lastExportReportText = _lastExportResult.ReportText;

            if (_lastExportResult.Success && promptForFolder)
            {
                _lastExportReportText = AppendWriteStatus(_lastExportReportText, WriteExportPackage(_lastExportResult));
            }

            _lastReport = _lastExportResult.ValidationReport;
            _lastReportText = BuildReportText(_lastReport);
            ApplyValidationReportToUi(_lastReport, _lastExportReportText);

            if (_validationLabel != null)
            {
                if (_lastExportResult.Success)
                {
                    _validationLabel.text = "\u2705 导出 JSON 已生成：" + _lastExportResult.Package.FileCount + " files";
                    _validationLabel.style.color = _lastReport != null && _lastReport.IssueCount > 0
                        ? UiColors.WarnAmber
                        : UiColors.SuccessGreen;
                }
                else
                {
                    _validationLabel.text = "\u274C 导出 JSON 失败：validation gate";
                    _validationLabel.style.color = UiColors.ErrorRed;
                }
            }
        }

        private void CopyExportReport()
        {
            if (string.IsNullOrEmpty(_lastExportReportText))
            {
                ExportJsonPackage(false);
            }

            EditorGUIUtility.systemCopyBuffer = _lastExportReportText;
        }

        private static string AppendWriteStatus(string reportText, string writeStatus)
        {
            if (string.IsNullOrEmpty(writeStatus))
            {
                return reportText;
            }

            return (reportText ?? string.Empty) + Environment.NewLine + "Editor Write:" + Environment.NewLine + writeStatus;
        }

        private static string WriteExportPackage(CombatAuthoringExportResult result)
        {
            string selectedFolder = EditorUtility.SaveFolderPanel(
                "Export Combat Authoring JSON Package",
                Application.dataPath,
                result.Context.PackageId);
            if (string.IsNullOrEmpty(selectedFolder))
            {
                return "Skipped: no folder selected. Package exists in memory and file paths are listed above.";
            }

            try
            {
                CombatAuthoringExportFile[] files = result.Files;
                for (int i = 0; i < files.Length; i++)
                {
                    string filePath = Path.Combine(selectedFolder, files[i].Path.Replace('/', Path.DirectorySeparatorChar));
                    string directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(filePath, files[i].Content, Encoding.UTF8);
                }

                RefreshAssetDatabaseIfProjectPath(selectedFolder);
                return "Wrote " + files.Length + " files to: " + selectedFolder;
            }
            catch (Exception ex)
            {
                return "Write failed: " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static void RefreshAssetDatabaseIfProjectPath(string selectedFolder)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullSelectedFolder = Path.GetFullPath(selectedFolder);
            if (fullSelectedFolder.StartsWith(projectRoot, StringComparison.Ordinal))
            {
                AssetDatabase.Refresh();
            }
        }

        private void PreviewExplain()
        {
            var resolver = new SceneMarkerResolver(_sceneBindingAsset);
            int frame = CombatAuthoringSceneState.Frame;
            _lastPreviewReport = CombatAuthoringPreviewExplainer.Explain(_actionAsset, _sceneBindingAsset, frame, resolver);
            _lastPreviewText = _lastPreviewReport.ToDisplayText();
            if (_previewExplain != null)
            {
                _previewExplain.value = _lastPreviewText;
            }
        }

        private void CopyExplain()
        {
            if (string.IsNullOrEmpty(_lastPreviewText))
            {
                PreviewExplain();
            }

            EditorGUIUtility.systemCopyBuffer = _lastPreviewText;
        }

        private void RunShowcasePreview()
        {
            if (_actionSerialized != null)
                _actionSerialized.ApplyModifiedProperties();
            if (_bindingSerialized != null)
                _bindingSerialized.ApplyModifiedProperties();

            _lastReport = CombatAuthoringValidator.Validate(_actionAsset, _sceneBindingAsset);
            _lastReportText = BuildReportText(_lastReport);
            ApplyValidationReportToUi(_lastReport, _lastReportText);

            if (_lastReport != null && _lastReport.HasErrors)
            {
                CombatAuthoringIssue firstError = FirstIssue(CombatAuthoringSeverity.Error);
                string message = "Showcase 预览已阻断：validation 存在 Error。请先处理 "
                    + firstError.Section + " / " + firstError.Field + "，建议：" + firstError.SuggestedFix;
                SetQuickActionStatus(message, UiColors.ErrorRed);
                return;
            }

            if (CombatAuthoringShowcasePlaySession.Begin(_actionAsset, _sceneBindingAsset, _lastReport, out string status))
            {
                SetQuickActionStatus(status, UiColors.SuccessGreen);
            }
            else
            {
                SetQuickActionStatus(status, UiColors.ErrorRed);
            }

            RefreshContext();
        }

        private void ClearShowcasePreviewSession()
        {
            CombatAuthoringShowcasePlaySession.Clear();
            SetQuickActionStatus(CombatAuthoringShowcasePlaySession.LastStatus, UiColors.TextSecondary);
            RefreshContext();
        }

        private CombatAuthoringIssue FirstIssue(CombatAuthoringSeverity severity)
        {
            if (_lastReport == null)
                return default;

            for (int i = 0; i < _lastReport.IssueCount; i++)
            {
                CombatAuthoringIssue issue = _lastReport.GetIssue(i);
                if (issue.Severity == severity)
                    return issue;
            }

            return default;
        }

        private static string BuildReportText(CombatAuthoringReport report)
        {
            return CombatAuthoringExportReport.BuildValidationText(report);
        }

        private static string IssueSeverityIcon(CombatAuthoringSeverity severity)
        {
            switch (severity)
            {
                case CombatAuthoringSeverity.Error:
                    return "\u274C";
                case CombatAuthoringSeverity.Warning:
                    return "\u26A0";
                default:
                    return "\u2139";
            }
        }

        private static Color IssueSeverityColor(CombatAuthoringSeverity severity)
        {
            switch (severity)
            {
                case CombatAuthoringSeverity.Error:
                    return UiColors.ErrorRed;
                case CombatAuthoringSeverity.Warning:
                    return UiColors.WarnAmber;
                default:
                    return UiColors.TextSecondary;
            }
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static string BuildPackageId(CombatActionAuthoringAsset action)
        {
            int actionId = action == null ? 0 : action.ActionId;
            return "combat_authoring_action_" + actionId;
        }

        private static string FormatRange(CombatAuthoringFrameRange range)
        {
            return range.IsEmpty ? "empty" : range.StartFrame + "-" + range.EndFrame;
        }

        private void UpdateTimelinePlayhead()
        {
            if (_timelinePlayhead == null || _actionAsset == null)
            {
                return;
            }

            int totalFrames = Math.Max(1, _actionAsset.TotalFrames);
            float trackWidth = GetTimelineTrackWidth(totalFrames);
            float frameX = FrameToTimelineX(CombatAuthoringSceneState.Frame, totalFrames, trackWidth);
            _timelinePlayhead.style.left = TimelineStripLabelWidth + frameX;
        }

        private static float FrameToTimelineX(int frame, int totalFrames, float trackWidth)
        {
            int clampedFrame = Math.Max(0, Math.Min(frame, Math.Max(0, totalFrames - 1)));
            return clampedFrame * GetTimelineFrameWidth(totalFrames, trackWidth);
        }

        private static float GetTimelineFrameWidth(int totalFrames, float trackWidth)
        {
            return trackWidth / Math.Max(1, totalFrames);
        }

        private static int GetTimelineTickStep(int totalFrames)
        {
            if (totalFrames <= 12)
            {
                return 1;
            }

            if (totalFrames <= 30)
            {
                return 5;
            }

            if (totalFrames <= 90)
            {
                return 10;
            }

            return 15;
        }

    }
}
