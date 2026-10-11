using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private void CreateDefaultProfile()
        {
            EnsureProfileFolder();
            string path = CharacterTestRuntimeAiPlannerProfileJson.DefaultProjectAssetPath;
            string fullPath = ResolveProjectFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            if (File.Exists(fullPath)
                && !EditorUtility.DisplayDialog(
                    "覆盖 Runtime AI Planner JSON",
                    "默认 Runtime AI Planner JSON 已存在，是否覆盖为默认近战模板？",
                    "覆盖",
                    "取消"))
            {
                LoadProfileFromProjectPath(path);
                return;
            }

            File.WriteAllText(
                fullPath,
                CharacterTestRuntimeAiPlannerProfileJson.ToJson(
                    CharacterTestRuntimeAiPlannerProfileSource.CreateDefault()));
            AssetDatabase.ImportAsset(path);
            _profilePicker?.ReloadProfiles();
            LoadProfileFromProjectPath(path);
            ValidateProfile();
        }

        private void CreatePerceptionAwareMeleeProfile()
        {
            EnsureProfileFolder();
            string path = CharacterTestRuntimeAiPlannerEditorModel.PerceptionAwareMeleeProjectAssetPath;
            string fullPath = ResolveProjectFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            if (File.Exists(fullPath)
                && !EditorUtility.DisplayDialog(
                    "覆盖 Runtime AI Planner JSON",
                    "感知版近战模板 JSON 已存在，是否覆盖？",
                    "覆盖",
                    "取消"))
            {
                LoadProfileFromProjectPath(path);
                return;
            }

            File.WriteAllText(
                fullPath,
                CharacterTestRuntimeAiPlannerProfileJson.ToJson(
                    CharacterTestRuntimeAiPlannerEditorModel.CreatePerceptionAwareMeleeTemplate()));
            AssetDatabase.ImportAsset(path);
            _profilePicker?.ReloadProfiles();
            LoadProfileFromProjectPath(path);
            ValidateProfile();
        }

        private void ShowTemplateMenu()
        {
            var menu = new GenericMenu();
            IReadOnlyList<CharacterTestRuntimeAiPlannerTemplateDescriptor> templates =
                CharacterTestRuntimeAiPlannerTemplateLibrary.Templates;
            for (int i = 0; i < templates.Count; i++)
            {
                CharacterTestRuntimeAiPlannerTemplateDescriptor template = templates[i];
                menu.AddItem(
                    new GUIContent(template.DisplayName),
                    false,
                    () => CreateProfileTemplate(template.TemplateId));
            }

            menu.ShowAsContext();
        }

        private void CreateProfileTemplate(string templateId)
        {
            if (!CharacterTestRuntimeAiPlannerTemplateLibrary.TryGetTemplate(
                    templateId,
                    out CharacterTestRuntimeAiPlannerTemplateDescriptor descriptor))
            {
                ShowNotification(new GUIContent("未知 Runtime AI Planner 模板"));
                return;
            }

            EnsureProfileFolder();
            string path = descriptor.ProjectAssetPath;
            string fullPath = ResolveProjectFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            if (File.Exists(fullPath)
                && !EditorUtility.DisplayDialog(
                    "覆盖 Runtime AI Planner JSON",
                    descriptor.DisplayName + " JSON 已存在，是否覆盖？",
                    "覆盖",
                    "取消"))
            {
                LoadProfileFromProjectPath(path);
                return;
            }

            File.WriteAllText(
                fullPath,
                CharacterTestRuntimeAiPlannerProfileJson.ToJson(
                    CharacterTestRuntimeAiPlannerTemplateLibrary.CreateTemplate(templateId)));
            AssetDatabase.ImportAsset(path);
            _profilePicker?.ReloadProfiles();
            LoadProfileFromProjectPath(path);
            ValidateProfile();
        }

        private void OpenSelectedProfile()
        {
            if (string.IsNullOrWhiteSpace(_profileProjectPath))
            {
                ShowNotification(new GUIContent("未选择 Runtime AI Planner profile"));
                return;
            }

            LoadProfileFromProjectPath(_profileProjectPath);
        }

        private void ChooseProfileJson()
        {
            string currentFullPath = ResolveProjectFullPath(_profileProjectPath);
            string startDirectory = Directory.Exists(Path.GetDirectoryName(currentFullPath))
                ? Path.GetDirectoryName(currentFullPath)
                : Application.dataPath;
            string selected = EditorUtility.OpenFilePanel(
                "选择 Runtime AI Planner JSON",
                startDirectory,
                "json");
            if (string.IsNullOrWhiteSpace(selected))
                return;

            string projectPath = ToProjectAssetPath(selected);
            if (!projectPath.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
            {
                ShowNotification(new GUIContent("请选择 Assets/StreamingAssets 目录内的 JSON 文件"));
                return;
            }

            _profilePicker?.ReloadProfiles();
            LoadProfileFromProjectPath(projectPath);
        }

        private void LoadSceneProfile()
        {
            CharacterTestSceneProfile sceneProfile = FindSceneProfile();
            if (sceneProfile == null || string.IsNullOrWhiteSpace(sceneProfile.RuntimeAiPlannerProfilePath))
            {
                ShowNotification(new GUIContent("当前场景 Profile 未设置 Runtime AI Planner JSON 路径"));
                return;
            }

            LoadProfileFromProjectPath(ToProjectAssetPath(sceneProfile.RuntimeAiPlannerProfilePath));
        }

        private void BindToSceneProfile()
        {
            if (string.IsNullOrWhiteSpace(_profileProjectPath))
            {
                ShowNotification(new GUIContent("未选择 Profile JSON"));
                return;
            }

            if (!_profileProjectPath.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
            {
                ShowNotification(new GUIContent("场景默认 Profile 必须位于 Assets/StreamingAssets 下"));
                return;
            }

            if (!CharacterTestRuntimeAiPlannerProfilePicker.TryGetProfileEntry(
                    _profileProjectPath,
                    out CharacterTestRuntimeAiPlannerProfilePickerEntry entry)
                || !entry.IsValid)
            {
                ShowNotification(new GUIContent("Profile 校验失败，不能设为场景默认"));
                return;
            }

            CharacterTestSceneProfile sceneProfile = FindSceneProfile();
            if (sceneProfile == null)
            {
                ShowNotification(new GUIContent("未找到 CharacterTestSceneProfile Asset"));
                return;
            }

            var serialized = new SerializedObject(sceneProfile);
            SerializedProperty property = serialized.FindProperty("_runtimeAiPlannerProfilePath");
            if (property == null)
            {
                ShowNotification(new GUIContent("场景 Profile 缺少 Runtime AI Planner 字段"));
                return;
            }

            Undo.RecordObject(sceneProfile, "Bind Runtime AI Planner JSON Profile");
            property.stringValue = ToStreamingAssetsRelativePath(_profileProjectPath);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(sceneProfile);
            AssetDatabase.SaveAssets();
            _statusText = "已绑定场景路径";
            RefreshView();
        }

        private void CopySelectedProfileAsNew()
        {
            if (string.IsNullOrWhiteSpace(_profileProjectPath))
            {
                ShowNotification(new GUIContent("未选择 Runtime AI Planner profile"));
                return;
            }

            string sourceFullPath = ResolveProjectFullPath(_profileProjectPath);
            if (!File.Exists(sourceFullPath) && !_profileLoaded)
            {
                ShowNotification(new GUIContent("当前 Profile JSON 文件不存在"));
                return;
            }

            EnsureProfileFolder();
            string targetProjectPath = CreateAvailableCopyProjectPath(_profileProjectPath);
            string targetFullPath = ResolveProjectFullPath(targetProjectPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath));
            if (_profileLoaded)
            {
                File.WriteAllText(
                    targetFullPath,
                    CharacterTestRuntimeAiPlannerProfileJson.ToJson(_model.ToRuntimeProfile()));
            }
            else
            {
                File.Copy(sourceFullPath, targetFullPath, overwrite: false);
            }

            AssetDatabase.ImportAsset(targetProjectPath);
            _profilePicker?.ReloadProfiles();
            LoadProfileFromProjectPath(targetProjectPath);
            _statusText = "已复制为 " + Path.GetFileName(targetProjectPath);
            RefreshView();
        }

        private void ValidateProfile()
        {
            if (!_profileLoaded)
            {
                _lastReport = "Runtime AI Planner profile JSON is not loaded.";
                _statusText = "未加载 Profile JSON";
                ShowNotification(new GUIContent("未加载 Profile JSON"));
                RefreshView();
                return;
            }

            CharacterTestRuntimeAiPlannerValidationReport report = _model.Validate();
            _lastReport = report.ToText();
            _statusText = report.Success ? "校验通过" : "校验存在错误";
            ShowNotification(new GUIContent(_statusText));
            if (_activeTab == ValidationTabIndex)
                RefreshView();
            else
                UpdateStatusLabel();
        }

        private void SaveProfile()
        {
            if (!_profileLoaded)
            {
                ShowNotification(new GUIContent("未加载 Profile JSON"));
                return;
            }

            EnsureProfileFolder();
            string fullPath = ResolveProjectFullPath(_profileProjectPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(
                fullPath,
                CharacterTestRuntimeAiPlannerProfileJson.ToJson(_model.ToRuntimeProfile()));
            AssetDatabase.ImportAsset(_profileProjectPath);
            _profilePicker?.ReloadProfiles();
            _dirty = false;
            CharacterTestRuntimeAiPlannerValidationReport report = _model.Validate();
            _lastReport = report.ToText();
            _statusText = report.Success ? "已保存" : "已保存，校验存在错误";
            ShowNotification(new GUIContent(_statusText));
            RefreshView();
        }

        private void CopyReport()
        {
            if (_activeTab == PlayMonitorTabIndex)
                _lastReport = CreatePlayMonitorReportText();
            else if (string.IsNullOrEmpty(_lastReport))
                ValidateProfile();
            if (string.IsNullOrEmpty(_lastReport))
                _lastReport = "Runtime AI Planner report is empty.";

            EditorGUIUtility.systemCopyBuffer = _lastReport;
            ShowNotification(new GUIContent("报告已复制"));
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null)
                return;

            string status = !_profileLoaded
                ? "未加载"
                : (_dirty ? "有未保存修改" : _statusText);
            if (string.IsNullOrWhiteSpace(status))
                status = _profileLoaded ? "已加载" : "未加载";

            _statusLabel.text = status + " | 保存后重建 CharacterTest 会话生效";
        }

        private static CharacterTestSceneProfile FindSceneProfile()
        {
            CharacterTestSceneProfile sceneProfile =
                Selection.activeObject as CharacterTestSceneProfile;
            if (sceneProfile != null)
                return sceneProfile;

            return AssetDatabase.LoadAssetAtPath<CharacterTestSceneProfile>(
                CharacterTestSceneProfile.DefaultAssetPath);
        }

        private static void EnsureProfileFolder()
        {
            const string folder = CharacterTestRuntimeAiPlannerTemplateLibrary.ProfileFolderProjectPath;
            if (AssetDatabase.IsValidFolder(folder))
                return;

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        private void InitializeProfilePathFromSceneDefault()
        {
            CharacterTestSceneProfile sceneProfile = FindSceneProfile();
            if (sceneProfile != null && !string.IsNullOrWhiteSpace(sceneProfile.RuntimeAiPlannerProfilePath))
            {
                _profileProjectPath = ToProjectAssetPath(sceneProfile.RuntimeAiPlannerProfilePath);
                return;
            }

            if (string.IsNullOrWhiteSpace(_profileProjectPath))
                _profileProjectPath = CharacterTestRuntimeAiPlannerProfileJson.DefaultProjectAssetPath;
        }

        private static string CreateAvailableCopyProjectPath(string sourceProjectPath)
        {
            string normalized = NormalizeProjectAssetPath(sourceProjectPath);
            string fileName = Path.GetFileNameWithoutExtension(normalized);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "runtime_ai_planner";

            string folder = CharacterTestRuntimeAiPlannerTemplateLibrary.ProfileFolderProjectPath;
            for (int i = 1; i < 1000; i++)
            {
                string suffix = i == 1 ? "_copy" : "_copy_" + i.ToString();
                string candidate = folder + "/" + fileName + suffix + ".json";
                if (!File.Exists(ResolveProjectFullPath(candidate)))
                    return candidate;
            }

            return folder + "/" + fileName + "_copy_" + DateTime.UtcNow.Ticks.ToString() + ".json";
        }

        private static string NormalizeProjectAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return CharacterTestRuntimeAiPlannerProfileJson.DefaultProjectAssetPath;

            string normalized = path.Replace('\\', '/').Trim();
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return normalized;

            if (normalized.StartsWith("MxFramework/", StringComparison.OrdinalIgnoreCase))
                return "Assets/StreamingAssets/" + normalized;

            return normalized;
        }

        private static string ToProjectAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return CharacterTestRuntimeAiPlannerProfileJson.DefaultProjectAssetPath;

            string normalized = path.Replace('\\', '/').Trim();
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/');
            if (normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(projectRoot.Length + 1);

            return NormalizeProjectAssetPath(normalized);
        }

        private static string ToStreamingAssetsRelativePath(string projectAssetPath)
        {
            string normalized = NormalizeProjectAssetPath(projectAssetPath);
            const string prefix = "Assets/StreamingAssets/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(prefix.Length);

            return CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(normalized);
        }

        private static string ResolveProjectFullPath(string projectAssetPath)
        {
            string normalized = NormalizeProjectAssetPath(projectAssetPath);
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));
        }
    }
}
