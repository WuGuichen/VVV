using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal readonly struct CharacterTestRuntimeAiPlannerProfilePickerEntry
    {
        public CharacterTestRuntimeAiPlannerProfilePickerEntry(
            bool isInherit,
            string projectAssetPath,
            string runtimePath,
            string displayName,
            string fileName,
            string profileId,
            bool isValid,
            string reportText)
        {
            IsInherit = isInherit;
            ProjectAssetPath = projectAssetPath ?? string.Empty;
            RuntimePath = runtimePath ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            FileName = fileName ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            IsValid = isValid;
            ReportText = reportText ?? string.Empty;
        }

        public bool IsInherit { get; }
        public string ProjectAssetPath { get; }
        public string RuntimePath { get; }
        public string DisplayName { get; }
        public string FileName { get; }
        public string ProfileId { get; }
        public bool IsValid { get; }
        public string ReportText { get; }
        public bool CanBind => IsInherit || IsValid;

        public string ShortLabel
        {
            get
            {
                if (IsInherit)
                    return DisplayName;
                if (string.IsNullOrWhiteSpace(DisplayName))
                    return FileName;
                return DisplayName + " | " + FileName;
            }
        }

        public string Label
        {
            get
            {
                if (IsInherit)
                    return DisplayName;

                string label = ShortLabel;
                if (!string.IsNullOrWhiteSpace(ProfileId))
                    label += " | " + ProfileId;
                label += IsValid ? " | 通过" : " | 校验失败";
                return label;
            }
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerProfilePicker : VisualElement
    {
        public const string PopupName = "runtime-ai-planner-profile-picker-popup";
        public const string SearchName = "runtime-ai-planner-profile-picker-search";
        public const string RefreshButtonName = "runtime-ai-planner-profile-picker-refresh";

        private readonly bool _includeInherit;
        private readonly string _inheritLabel;
        private readonly string _popupName;
        private readonly Action<CharacterTestRuntimeAiPlannerProfilePickerEntry> _onSelected;
        private readonly TextField _searchField;
        private readonly VisualElement _popupHost;
        private readonly Label _statusLabel;
        private readonly List<CharacterTestRuntimeAiPlannerProfilePickerEntry> _allEntries =
            new List<CharacterTestRuntimeAiPlannerProfilePickerEntry>();
        private readonly List<CharacterTestRuntimeAiPlannerProfilePickerEntry> _visibleEntries =
            new List<CharacterTestRuntimeAiPlannerProfilePickerEntry>();
        private readonly List<string> _visibleLabels = new List<string>();

        private string _selectedProjectPath = string.Empty;
        private string _searchText = string.Empty;
        private PopupField<string> _popup;

        public CharacterTestRuntimeAiPlannerProfilePicker(
            string label,
            string selectedProjectPath,
            bool includeInherit,
            string inheritLabel,
            Action<CharacterTestRuntimeAiPlannerProfilePickerEntry> onSelected,
            string popupName = PopupName)
        {
            _includeInherit = includeInherit;
            _inheritLabel = string.IsNullOrWhiteSpace(inheritLabel) ? "继承场景默认" : inheritLabel;
            _popupName = string.IsNullOrWhiteSpace(popupName) ? PopupName : popupName;
            _onSelected = onSelected;
            _selectedProjectPath = includeInherit && string.IsNullOrWhiteSpace(selectedProjectPath)
                ? string.Empty
                : NormalizeProjectAssetPath(selectedProjectPath);

            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 1;
            style.minWidth = 0;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0;

            _popupHost = new VisualElement();
            _popupHost.style.flexGrow = 1;
            _popupHost.style.flexShrink = 1;
            _popupHost.style.minWidth = 0;
            row.Add(_popupHost);

            Button refresh = new Button(ReloadProfiles)
            {
                name = RefreshButtonName,
                text = "刷新"
            };
            refresh.style.marginLeft = 4;
            row.Add(refresh);
            Add(row);

            _searchField = new TextField("搜索")
            {
                name = SearchName,
                isDelayed = true
            };
            _searchField.style.marginTop = 2;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue ?? string.Empty;
                RebuildPopup(label);
            });
            Add(_searchField);

            _statusLabel = new Label();
            _statusLabel.style.marginTop = 2;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            Add(_statusLabel);

            ReloadProfiles(label);
        }

        public CharacterTestRuntimeAiPlannerProfilePickerEntry SelectedEntry { get; private set; }

        public void SetSelectedProjectPath(string projectAssetPath)
        {
            string normalized = _includeInherit && string.IsNullOrWhiteSpace(projectAssetPath)
                ? string.Empty
                : NormalizeProjectAssetPath(projectAssetPath);
            if (string.Equals(_selectedProjectPath, normalized, StringComparison.Ordinal))
                return;

            _selectedProjectPath = normalized;
            RebuildPopup(null);
        }

        public void ReloadProfiles()
        {
            ReloadProfiles(null);
        }

        private void ReloadProfiles(string label)
        {
            _allEntries.Clear();
            if (_includeInherit)
            {
                _allEntries.Add(new CharacterTestRuntimeAiPlannerProfilePickerEntry(
                    true,
                    string.Empty,
                    string.Empty,
                    _inheritLabel,
                    string.Empty,
                    string.Empty,
                    true,
                    string.Empty));
            }

            AddKnownTemplateEntries(_allEntries);
            AddCustomEntries(_allEntries);
            RebuildPopup(label);
        }

        private void RebuildPopup(string labelOverride)
        {
            _visibleEntries.Clear();
            _visibleLabels.Clear();

            for (int i = 0; i < _allEntries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerProfilePickerEntry entry = _allEntries[i];
                if (MatchesSearch(entry, _searchText))
                {
                    _visibleEntries.Add(entry);
                    _visibleLabels.Add(entry.Label);
                }
            }

            if (_visibleEntries.Count == 0)
                _visibleLabels.Add("无匹配行为 Profile");

            int selectedIndex = FindVisibleSelectedIndex();
            _popupHost.Clear();
            _popup = new PopupField<string>(
                string.IsNullOrWhiteSpace(labelOverride) ? "行为 Profile" : labelOverride,
                _visibleLabels,
                selectedIndex)
            {
                name = _popupName
            };
            _popup.style.flexGrow = 1;
            _popup.style.flexShrink = 1;
            _popup.style.minWidth = 0;
            _popup.RegisterValueChangedCallback(_ => SelectVisibleEntry(_popup.index));
            _popupHost.Add(_popup);

            SelectVisibleEntry(selectedIndex, notify: false);
            UpdateStatusLabel();
        }

        private int FindVisibleSelectedIndex()
        {
            for (int i = 0; i < _visibleEntries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerProfilePickerEntry entry = _visibleEntries[i];
                if (entry.IsInherit && string.IsNullOrEmpty(_selectedProjectPath))
                    return i;
                if (!entry.IsInherit
                    && string.Equals(entry.ProjectAssetPath, _selectedProjectPath, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }

        private void SelectVisibleEntry(int index, bool notify = true)
        {
            if (_visibleEntries.Count == 0)
                return;

            int selectedIndex = Mathf.Clamp(index, 0, _visibleEntries.Count - 1);
            SelectedEntry = _visibleEntries[selectedIndex];
            _selectedProjectPath = SelectedEntry.ProjectAssetPath;
            UpdateStatusLabel();
            if (!notify)
                return;

            _onSelected?.Invoke(SelectedEntry);
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null)
                return;

            if (_visibleEntries.Count == 0)
            {
                _statusLabel.text = "未找到匹配的 Runtime AI Planner profile。";
                return;
            }

            if (SelectedEntry.IsInherit)
            {
                _statusLabel.text = "空路径：继承场景默认 Runtime AI Planner profile。";
                return;
            }

            _statusLabel.text = SelectedEntry.IsValid
                ? "已选择：" + SelectedEntry.RuntimePath
                : "校验失败：" + FirstLine(SelectedEntry.ReportText);
        }

        private static bool MatchesSearch(
            CharacterTestRuntimeAiPlannerProfilePickerEntry entry,
            string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            string value = search.Trim();
            return Contains(entry.DisplayName, value)
                || Contains(entry.FileName, value)
                || Contains(entry.ProfileId, value)
                || Contains(entry.ProjectAssetPath, value)
                || Contains(entry.RuntimePath, value);
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrEmpty(source)
                && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddKnownTemplateEntries(
            List<CharacterTestRuntimeAiPlannerProfilePickerEntry> entries)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerTemplateDescriptor> templates =
                CharacterTestRuntimeAiPlannerTemplateLibrary.Templates;
            for (int i = 0; i < templates.Count; i++)
            {
                CharacterTestRuntimeAiPlannerTemplateDescriptor template = templates[i];
                string projectPath = NormalizeProjectAssetPath(template.ProjectAssetPath);
                if (!File.Exists(ResolveProjectFullPath(projectPath)))
                    continue;

                entries.Add(CreateEntry(projectPath, template.DisplayName));
            }
        }

        private static void AddCustomEntries(
            List<CharacterTestRuntimeAiPlannerProfilePickerEntry> entries)
        {
            string folder = ResolveProjectFullPath(
                CharacterTestRuntimeAiPlannerTemplateLibrary.ProfileFolderProjectPath);
            if (!Directory.Exists(folder))
                return;

            string[] files = Directory.GetFiles(folder, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                string projectPath = ToProjectAssetPath(files[i]);
                if (ContainsEntry(entries, projectPath))
                    continue;

                entries.Add(CreateEntry(projectPath, "自定义"));
            }
        }

        private static bool ContainsEntry(
            List<CharacterTestRuntimeAiPlannerProfilePickerEntry> entries,
            string projectPath)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].ProjectAssetPath, projectPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static CharacterTestRuntimeAiPlannerProfilePickerEntry CreateEntry(
            string projectPath,
            string displayName)
        {
            string fullPath = ResolveProjectFullPath(projectPath);
            string fileName = Path.GetFileName(projectPath);
            string runtimePath = ToStreamingAssetsRelativePath(projectPath);
            string profileId = string.Empty;
            bool valid = false;
            string reportText = "Profile JSON 文件不存在。";

            if (File.Exists(fullPath))
            {
                try
                {
                    string json = File.ReadAllText(fullPath);
                    valid = CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                        json,
                        out CharacterTestRuntimeAiPlannerProfile profile,
                        out CharacterTestRuntimeAiPlannerValidationReport report);
                    profileId = profile != null ? profile.ProfileId : string.Empty;
                    reportText = report != null ? report.ToText() : string.Empty;
                }
                catch (IOException exception)
                {
                    reportText = "Profile JSON read failed: " + exception.Message;
                }
                catch (UnauthorizedAccessException exception)
                {
                    reportText = "Profile JSON read denied: " + exception.Message;
                }
            }

            return new CharacterTestRuntimeAiPlannerProfilePickerEntry(
                false,
                NormalizeProjectAssetPath(projectPath),
                runtimePath,
                displayName,
                fileName,
                profileId,
                valid,
                reportText);
        }

        public static string BuildInheritLabel(string sceneRuntimePath)
        {
            string normalizedRuntimePath =
                CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(sceneRuntimePath);
            if (string.IsNullOrWhiteSpace(normalizedRuntimePath))
                return "继承场景默认（未设置）";

            string projectPath = NormalizeProjectAssetPath(normalizedRuntimePath);
            CharacterTestRuntimeAiPlannerProfilePickerEntry entry = CreateEntry(projectPath, FindTemplateDisplayName(projectPath));
            return "继承场景默认：" + entry.ShortLabel;
        }

        public static bool TryGetProfileEntry(
            string projectAssetPath,
            out CharacterTestRuntimeAiPlannerProfilePickerEntry entry)
        {
            string normalized = NormalizeProjectAssetPath(projectAssetPath);
            string displayName = FindTemplateDisplayName(normalized);
            entry = CreateEntry(normalized, string.IsNullOrWhiteSpace(displayName) ? "自定义" : displayName);
            return File.Exists(ResolveProjectFullPath(normalized));
        }

        internal static IReadOnlyList<CharacterTestRuntimeAiPlannerProfilePickerEntry> ScanProfilesForTests()
        {
            var entries = new List<CharacterTestRuntimeAiPlannerProfilePickerEntry>();
            AddKnownTemplateEntries(entries);
            AddCustomEntries(entries);
            return entries;
        }

        public static string NormalizeProjectAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalized = path.Replace('\\', '/').Trim();
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return normalized;
            if (normalized.StartsWith("MxFramework/", StringComparison.OrdinalIgnoreCase))
                return "Assets/StreamingAssets/" + normalized;
            if (Path.IsPathRooted(normalized))
            {
                string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/');
                if (normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                    return normalized.Substring(projectRoot.Length + 1);
                return normalized;
            }
            return "Assets/StreamingAssets/" + normalized;
        }

        public static string ToProjectAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalized = path.Replace('\\', '/').Trim();
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/');
            if (normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(projectRoot.Length + 1);
            if (Path.IsPathRooted(normalized))
                return normalized;

            return NormalizeProjectAssetPath(normalized);
        }

        public static string ToStreamingAssetsRelativePath(string projectAssetPath)
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

        private static string FindTemplateDisplayName(string projectPath)
        {
            string normalized = NormalizeProjectAssetPath(projectPath);
            IReadOnlyList<CharacterTestRuntimeAiPlannerTemplateDescriptor> templates =
                CharacterTestRuntimeAiPlannerTemplateLibrary.Templates;
            for (int i = 0; i < templates.Count; i++)
            {
                if (string.Equals(
                        NormalizeProjectAssetPath(templates[i].ProjectAssetPath),
                        normalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return templates[i].DisplayName;
                }
            }

            return string.Empty;
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "未知错误";

            int index = text.IndexOfAny(new[] { '\r', '\n' });
            return index >= 0 ? text.Substring(0, index) : text;
        }
    }
}
