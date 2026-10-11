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
        private VisualElement CreateWorkspaceTabs()
        {
            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexWrap = Wrap.Wrap;
            tabs.style.marginBottom = 8;
            tabs.style.flexShrink = 0;

            tabs.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            tabs.style.paddingLeft = 4;
            tabs.style.paddingRight = 4;
            tabs.style.paddingTop = 4;
            tabs.style.paddingBottom = 4;
            tabs.style.borderTopLeftRadius = 14;
            tabs.style.borderTopRightRadius = 14;
            tabs.style.borderBottomLeftRadius = 14;
            tabs.style.borderBottomRightRadius = 14;
            tabs.style.alignSelf = Align.FlexStart;

            tabs.Add(CreateWorkspaceTabButton("概览", ConfigWorkspacePage.Overview));
            tabs.Add(CreateWorkspaceTabButton("字段", ConfigWorkspacePage.Fields));
            tabs.Add(CreateWorkspaceTabButton("行视图", ConfigWorkspacePage.Rows));
            tabs.Add(CreateWorkspaceTabButton("引用", ConfigWorkspacePage.References));
            return tabs;
        }

        private Button CreateWorkspaceTabButton(string text, ConfigWorkspacePage page)
        {
            return CreatePillTabButton(text, null, () =>
            {
                _configWorkspacePage = page;
                RefreshConfigView();
            }, _configWorkspacePage == page);
        }

        private void PopulateWorkspacePage(IConfigEditorSource source, ConfigSchema schema)
        {
            switch (_configWorkspacePage)
            {
                case ConfigWorkspacePage.Fields:
                    AddFieldsPage(schema);
                    break;
                case ConfigWorkspacePage.Rows:
                    AddRowsPage(source);
                    break;
                case ConfigWorkspacePage.References:
                    AddReferencesPage(schema);
                    break;
                default:
                    AddOverviewPage(source, schema);
                    break;
            }
        }

        private static VisualElement CreateOverviewPropertyRow(string key, string val, string iconName)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f);
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;

            var keyContainer = new VisualElement();
            keyContainer.style.flexDirection = FlexDirection.Row;
            keyContainer.style.alignItems = Align.Center;
            keyContainer.style.width = 120;

            var icon = CreateIcon(iconName, 12);
            if (icon != null)
                keyContainer.Add(icon);

            var keyLabel = new Label(key);
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.color = new Color(0.72f, 0.72f, 0.72f);
            keyLabel.style.marginLeft = 2;
            keyLabel.style.fontSize = 11;
            keyContainer.Add(keyLabel);
            row.Add(keyContainer);

            var valLabel = new Label(val);
            valLabel.style.color = new Color(0.88f, 0.88f, 0.88f);
            valLabel.style.flexGrow = 1;
            valLabel.style.whiteSpace = WhiteSpace.Normal;
            valLabel.style.fontSize = 11;
            row.Add(valLabel);

            return row;
        }

        private void AddOverviewPage(IConfigEditorSource source, ConfigSchema schema)
        {
            string idRange = schema.IdRange.IsValid
                ? schema.IdRange.MinInclusive + " - " + schema.IdRange.MaxInclusive
                : "未限制";

            var grid = new VisualElement();
            grid.style.paddingLeft = 4;
            grid.style.paddingRight = 4;

            grid.Add(CreateOverviewPropertyRow("数据源", source.Name, "d_FilterSelectedOnly"));
            grid.Add(CreateOverviewPropertyRow("来源类型", source.SourceType, "d_FolderOpened"));
            grid.Add(CreateOverviewPropertyRow("结构类型", schema.StructureKind.ToString(), "d_CustomTool"));
            grid.Add(CreateOverviewPropertyRow("表名", schema.TableName, "d_Toolbar"));
            grid.Add(CreateOverviewPropertyRow("行数", source.RowCount.ToString(), "d_PlayButton"));
            grid.Add(CreateOverviewPropertyRow("字段数", schema.Fields.Count.ToString(), "d_Searchify"));
            grid.Add(CreateOverviewPropertyRow("引用规则数", schema.ReferenceRules.Count.ToString(), "d_Linked"));
            grid.Add(CreateOverviewPropertyRow("显示名", schema.DisplayName, "d_Settings"));
            grid.Add(CreateOverviewPropertyRow("ID 范围", idRange, "d_Profiler"));
            grid.Add(CreateOverviewPropertyRow("说明", schema.Description, "d_TextAsset Icon"));

            _configDetailsRoot.Add(grid);

            if (schema.RequiredLocales.Count > 0)
            {
                var locales = new Label("必需语言：" + JoinLocales(schema.RequiredLocales));
                locales.style.marginTop = 10;
                locales.style.fontSize = 11;
                locales.style.color = new Color(0.75f, 0.75f, 0.75f);
                _configDetailsRoot.Add(locales);
            }

            var note = new Label("编辑状态：只读预览，后续 Table Editor / Graph Inspector 复用当前 Schema 和 SourceIndex。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 14;
            note.style.fontSize = 11;
            note.style.color = new Color(0.55f, 0.55f, 0.55f);
            _configDetailsRoot.Add(note);
        }

        private void AddFieldsPage(ConfigSchema schema)
        {
            _configDetailsRoot.Add(CreateConfigFieldRow("字段", "类型", "必填", "引用", "说明", isHeader: true));
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                string fieldName = field.Name;
                string reference = field.ReferenceRule.IsValid ? field.ReferenceRule.GetTargetDisplayName() : "-";

                bool isSelectedField = fieldName == _selectedConfigFieldName;

                VisualElement row = CreateConfigFieldRow(
                    CreateFieldLabel(field),
                    string.IsNullOrEmpty(field.EnumId) ? field.FieldType.ToString() : field.FieldType + "\n" + field.EnumId,
                    field.Required ? "是" : "否",
                    reference,
                    string.IsNullOrEmpty(field.Description) ? "-" : field.Description,
                    isHeader: false,
                    onClick: () => SelectConfigField(fieldName)
                );

                // 奇偶斑马底色与高亮
                Color rowBg = isSelectedField
                    ? new Color(0.15f, 0.35f, 0.6f, 0.2f)
                    : ((i % 2 == 1) ? new Color(0.16f, 0.16f, 0.16f) : Color.clear);

                row.style.backgroundColor = rowBg;

                if (isSelectedField)
                {
                    row.style.borderLeftWidth = 3;
                    row.style.borderLeftColor = new Color(0.23f, 0.51f, 0.96f);
                }

                row.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    row.style.backgroundColor = isSelectedField ? new Color(0.15f, 0.35f, 0.6f, 0.3f) : new Color(0.2f, 0.24f, 0.32f);
                });
                row.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    row.style.backgroundColor = rowBg;
                });

                _configDetailsRoot.Add(row);
            }
        }

        private void AddRowsPage(IConfigEditorSource source)
        {
            var title = new Label("行视图与控件映射");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            title.style.fontSize = 12;
            title.style.color = new Color(0.9f, 0.9f, 0.9f);
            _configDetailsRoot.Add(title);
            _configDetailsRoot.Add(CreateRowPreview(source, 5));
        }

        private void AddReferencesPage(ConfigSchema schema)
        {
            var title = new Label("引用映射分析");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            title.style.fontSize = 12;
            title.style.color = new Color(0.9f, 0.9f, 0.9f);
            _configDetailsRoot.Add(title);

            var references = new Label(CreateReferenceSummary(schema));
            references.style.whiteSpace = WhiteSpace.Normal;
            references.style.fontSize = 11;
            references.style.color = new Color(0.8f, 0.8f, 0.8f);
            references.style.paddingLeft = 4;
            _configDetailsRoot.Add(references);
        }

        private void SelectConfigField(string fieldName)
        {
            _selectedConfigFieldName = fieldName ?? string.Empty;
            IConfigEditorSource source = GetSelectedConfigSource();
            if (source != null)
                RefreshConfigFieldInspector(source);

            // 重新刷新 Fields 页面，这样选中的行高亮才会正确更新！
            if (_configWorkspacePage == ConfigWorkspacePage.Fields)
                RefreshConfigView();
        }

        private void RefreshConfigFieldInspector(IConfigEditorSource source)
        {
            if (_configFieldInspectorRoot == null)
                return;

            _configFieldInspectorRoot.Clear();
            if (source == null)
            {
                AddInspectorText("未选择配置源。");
                return;
            }

            ConfigField field = GetSelectedConfigField(source);
            if (string.IsNullOrEmpty(field.Name))
            {
                AddInspectorText("在字段页点击 `详情` 查看字段元数据、枚举候选和引用目标。");
                return;
            }

            // 结构化卡片渲染右侧检查器
            _configFieldInspectorRoot.Add(CreateOverviewPropertyRow("中文名", string.IsNullOrEmpty(field.DisplayName) ? "-" : field.DisplayName, "d_Settings"));
            _configFieldInspectorRoot.Add(CreateOverviewPropertyRow("字段名", field.Name, "d_Searchify"));
            _configFieldInspectorRoot.Add(CreateOverviewPropertyRow("类型", field.FieldType.ToString(), "d_CustomTool"));
            _configFieldInspectorRoot.Add(CreateOverviewPropertyRow("必填", field.Required ? "是" : "否", "d_Checkmark"));
            _configFieldInspectorRoot.Add(CreateOverviewPropertyRow("控件提示", ConfigEditorControlHints.GetControlHint(field), "d_Toolbar"));
            _configFieldInspectorRoot.Add(CreateOverviewPropertyRow("枚举ID", string.IsNullOrEmpty(field.EnumId) ? "-" : field.EnumId, "d_Profiler"));

            string reference = field.ReferenceRule.IsValid ? field.ReferenceRule.GetTargetDisplayName() : "-";
            _configFieldInspectorRoot.Add(CreateOverviewPropertyRow("引用目标", reference, "d_Linked"));
            _configFieldInspectorRoot.Add(CreateOverviewPropertyRow("说明", string.IsNullOrEmpty(field.Description) ? "-" : field.Description, "d_TextAsset Icon"));

            if (!string.IsNullOrEmpty(field.EnumId) && source is IConfigEditorEnumProvider enumProvider && enumProvider.TryGetEnumDomain(field.EnumId, out ConfigEnumDomain domain))
            {
                var enumTitle = new Label("枚举候选值：");
                enumTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                enumTitle.style.marginTop = 10;
                enumTitle.style.marginBottom = 4;
                enumTitle.style.fontSize = 11;
                enumTitle.style.color = new Color(0.8f, 0.8f, 0.8f);
                _configFieldInspectorRoot.Add(enumTitle);

                string enumText = CreateEnumCandidateText(domain);
                var enumLabel = new Label(enumText);
                enumLabel.style.whiteSpace = WhiteSpace.Normal;
                enumLabel.style.paddingLeft = 8;
                enumLabel.style.fontSize = 11;
                enumLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                _configFieldInspectorRoot.Add(enumLabel);
            }

            if (field.ReferenceRule.IsValid)
            {
                var jumpBtn = CreateStyledButton("查看目标源", "d_Linked", () => JumpToReferenceTarget(field.ReferenceRule));
                jumpBtn.style.marginTop = 10;
                jumpBtn.style.alignSelf = Align.FlexStart;
                _configFieldInspectorRoot.Add(jumpBtn);
            }
        }

        private void AddInspectorText(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 11;
            label.style.color = new Color(0.65f, 0.65f, 0.65f);
            label.style.marginTop = 4;
            _configFieldInspectorRoot.Add(label);
        }

        private ConfigField GetSelectedConfigField(IConfigEditorSource source)
        {
            if (source == null || string.IsNullOrEmpty(_selectedConfigFieldName))
                return default;

            IReadOnlyList<ConfigField> fields = source.Schema.Fields;
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].Name == _selectedConfigFieldName)
                    return fields[i];
            }

            return default;
        }

        private static string CreateFieldInspectorText(IConfigEditorSource source, ConfigField field)
        {
            string reference = field.ReferenceRule.IsValid ? field.ReferenceRule.GetTargetDisplayName() : "-";
            var text =
                "中文名：" + (string.IsNullOrEmpty(field.DisplayName) ? "-" : field.DisplayName) +
                "\n字段名：" + field.Name +
                "\n类型：" + field.FieldType +
                "\n必填：" + (field.Required ? "是" : "否") +
                "\n控件：" + ConfigEditorControlHints.GetControlHint(field) +
                "\nenumId：" + (string.IsNullOrEmpty(field.EnumId) ? "-" : field.EnumId) +
                "\n引用：" + reference +
                "\n说明：" + (string.IsNullOrEmpty(field.Description) ? "-" : field.Description);

            if (!string.IsNullOrEmpty(field.EnumId) && source is IConfigEditorEnumProvider enumProvider && enumProvider.TryGetEnumDomain(field.EnumId, out ConfigEnumDomain domain))
                text += "\n候选项：\n" + CreateEnumCandidateText(domain);

            return text;
        }

        private static string CreateEnumCandidateText(ConfigEnumDomain domain)
        {
            if (domain == null || domain.Values.Count == 0)
                return "-";

            string text = string.Empty;
            for (int i = 0; i < domain.Values.Count; i++)
            {
                ConfigEnumValue value = domain.Values[i];
                if (i > 0)
                    text += "\n";
                text += "- " + (string.IsNullOrEmpty(value.DisplayName) ? value.Name : value.DisplayName + " " + value.Name) + " (" + value.Value + ")";
            }

            return text;
        }

        private void JumpToReferenceTarget(ConfigReferenceRule rule)
        {
            if (!rule.IsValid)
                return;

            EnsureConfigSources();
            for (int i = 0; i < _configSources.Count; i++)
            {
                IConfigEditorSource source = _configSources[i];
                if (source.Schema.TableName == rule.TargetSchemaName && source.Schema.StructureKind == rule.TargetStructureKind)
                {
                    _selectedConfigSourceName = source.Name;
                    _selectedConfigFieldName = rule.TargetKeyField;
                    _configWorkspacePage = ConfigWorkspacePage.Fields;
                    RefreshConfigView();
                    ShowNotification(new GUIContent("已定位目标源"));
                    return;
                }
            }

            ShowNotification(new GUIContent("未找到目标源"));
        }
    }
}
