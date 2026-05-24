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
        private VisualElement _configValidationBannerContainer;

        private VisualElement CreateConfigPanel()
        {
            EnsureConfigSources();

            var content = new VisualElement();
            content.style.flexGrow = 1;

            content.Add(CreateConfigContextBar());

            var workspace = new VisualElement();
            workspace.style.flexDirection = FlexDirection.Row;
            workspace.style.flexGrow = 1;
            workspace.style.minHeight = 350;

            var sourcePanel = CreatePanel("配置源");
            sourcePanel.style.width = 220;
            sourcePanel.style.flexShrink = 0;
            sourcePanel.style.marginRight = 8;
            sourcePanel.style.marginBottom = 8;

            var sourceScroll = new ScrollView(ScrollViewMode.Vertical);
            sourceScroll.style.flexGrow = 1;

            var sourceNote = new Label("按源选择当前工作对象。当前只读，不保存配置。");
            sourceNote.style.whiteSpace = WhiteSpace.Normal;
            sourceNote.style.color = new Color(0.5f, 0.5f, 0.5f);
            sourceNote.style.fontSize = 11;
            sourceNote.style.marginBottom = 8;
            sourceScroll.Add(sourceNote);
            AddConfigSourceButtons(sourceScroll);
            sourcePanel.Add(sourceScroll);

            var detailPanel = CreatePanel("主工作区");
            detailPanel.style.flexGrow = 1;
            detailPanel.style.marginRight = 8;
            detailPanel.style.marginBottom = 8;
            var detailScroll = new ScrollView(ScrollViewMode.Vertical);
            detailScroll.style.flexGrow = 1;
            _configDetailsRoot = new VisualElement();
            detailScroll.Add(_configDetailsRoot);
            detailPanel.Add(detailScroll);

            var reportPanel = CreatePanel("检查器 / 问题");
            reportPanel.style.width = 320;
            reportPanel.style.flexShrink = 0;
            reportPanel.style.marginBottom = 8;

            var reportScroll = new ScrollView(ScrollViewMode.Vertical);
            reportScroll.style.flexGrow = 1;

            reportScroll.Add(CreateButtonRow(
                CreateStyledButton("复制模板", "d_Copy", CopyConfigTemplate),
                CreateStyledButton("复制问题", "d_console.warnicon.sml", CopyConfigIssueList),
                CreateStyledButton("健康报告", "d_FilterSelectedOnly", CopyConfigHealthReport)));
            reportScroll.Add(CreateButtonRow(
                CreateStyledButton("变动报告", "d_Searchify", CopyConfigChangeReport),
                CreateStyledButton("重置基线", "d_Refresh", ResetConfigChangeBaseline)));

            var templateTitle = new Label("源预览");
            templateTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            templateTitle.style.marginTop = 8;
            templateTitle.style.marginBottom = 4;
            templateTitle.style.fontSize = 11;
            reportScroll.Add(templateTitle);

            _configTemplatePreview = new TextField();
            _configTemplatePreview.multiline = true;
            _configTemplatePreview.isReadOnly = true;
            _configTemplatePreview.style.height = 120;
            _configTemplatePreview.style.marginTop = 0;
            // 美化文本框背景与内边距
            _configTemplatePreview.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            _configTemplatePreview.style.paddingLeft = _configTemplatePreview.style.paddingRight = 6;
            _configTemplatePreview.style.paddingTop = _configTemplatePreview.style.paddingBottom = 4;
            _configTemplatePreview.style.borderTopWidth = _configTemplatePreview.style.borderRightWidth = _configTemplatePreview.style.borderBottomWidth = _configTemplatePreview.style.borderLeftWidth = 1;
            _configTemplatePreview.style.borderTopColor = _configTemplatePreview.style.borderRightColor = _configTemplatePreview.style.borderBottomColor = _configTemplatePreview.style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f);
            reportScroll.Add(_configTemplatePreview);

            _configValidationBannerContainer = new VisualElement();
            _configValidationBannerContainer.style.marginTop = 8;
            reportScroll.Add(_configValidationBannerContainer);

            // 兼容性占位
            _configValidationLabel = new Label();
            _configValidationLabel.style.display = DisplayStyle.None;
            reportScroll.Add(_configValidationLabel);

            var fieldTitle = new Label("字段详情");
            fieldTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            fieldTitle.style.marginTop = 10;
            fieldTitle.style.marginBottom = 4;
            fieldTitle.style.fontSize = 11;
            reportScroll.Add(fieldTitle);

            _configFieldInspectorRoot = new VisualElement();
            reportScroll.Add(_configFieldInspectorRoot);

            reportPanel.Add(reportScroll);

            workspace.Add(sourcePanel);
            workspace.Add(detailPanel);
            workspace.Add(reportPanel);

            content.Add(workspace);

            var issueDrawer = CreateIssueDrawer();
            issueDrawer.style.flexShrink = 0;
            issueDrawer.style.height = 150;
            content.Add(issueDrawer);

            return content;
        }

        private VisualElement CreateIssueDrawer()
        {
            var drawer = CreatePanel("问题抽屉");
            drawer.style.marginTop = 0;
            drawer.style.marginBottom = 8;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.flexWrap = Wrap.Wrap;
            header.style.alignItems = Align.Center;

            _configIssueFilterPopup = new PopupField<string>(
                "问题筛选",
                new List<string> { "全部", "Error", "Warning" },
                0);
            _configIssueFilterPopup.RegisterValueChangedCallback(_ => RefreshConfigIssuesView());
            _configIssueFilterPopup.style.minWidth = 180;
            _configIssueFilterPopup.style.height = 20;
            header.Add(_configIssueFilterPopup);

            var note = new Label("全局配置问题在这里集中查看，主工作区保持聚焦。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.color = new Color(0.55f, 0.55f, 0.55f);
            note.style.fontSize = 11;
            note.style.marginLeft = 8;
            header.Add(note);
            drawer.Add(header);

            _configIssuePreview = new TextField();
            _configIssuePreview.multiline = true;
            _configIssuePreview.isReadOnly = true;
            _configIssuePreview.style.flexGrow = 1;
            _configIssuePreview.style.marginTop = 6;
            _configIssuePreview.style.backgroundColor = new Color(0.09f, 0.09f, 0.09f);
            _configIssuePreview.style.borderTopColor = _configIssuePreview.style.borderRightColor = _configIssuePreview.style.borderBottomColor = _configIssuePreview.style.borderLeftColor = new Color(0.18f, 0.18f, 0.18f);
            _configIssuePreview.style.paddingLeft = _configIssuePreview.style.paddingRight = 6;
            _configIssuePreview.style.paddingTop = _configIssuePreview.style.paddingBottom = 4;
            drawer.Add(_configIssuePreview);
            return drawer;
        }

        private VisualElement CreateConfigContextBar()
        {
            var bar = CreatePanel("当前上下文");
            bar.style.marginBottom = 8;
            bar.style.flexShrink = 0;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;

            IConfigEditorSource source = GetSelectedConfigSource();
            string sourceName = source != null ? source.Name : "无配置源";
            string structure = source != null ? source.Schema.StructureKind.ToString() : "-";
            string status = _configHealth == null ? "未检测" : _configHealth.HasErrors ? "错误" : _configHealth.HasWarnings ? "警告" : "正常";

            // 采用精致高亮字和富文本显示
            string summary = "源：<b><color=#5c93e6>" + sourceName + "</color></b>    结构：<b><color=#bca6f2>" + structure + "</color></b>    状态：<b>" + GetStatusRichText(status) + "</b>";
            var label = new Label(summary);
            label.enableRichText = true;
            label.style.minWidth = 280;
            label.style.marginRight = 8;
            label.style.whiteSpace = WhiteSpace.Normal;
            row.Add(label);

            row.Add(CreateStyledButton("刷新", "d_Refresh", RefreshConfigSources));
            row.Add(CreateStyledButton("校验", "d_Checkmark", RefreshConfigValidation));
            row.Add(CreateStyledButton("提交前检查", "d_FilterSelectedOnly", RunConfigPrecommitCheck));
            row.Add(CreateStyledButton("AI 上下文", "d_CustomTool", CopyConfigAiFixContext));
            row.Add(CreateStyledButton("导出报告", "d_SaveAs", ExportConfigReportBundle));
            row.Add(CreateStyledButton("更多报告", "d_Linked", CopyConfigHealthReport));

            bar.Add(row);

            _configHealthLabel = new Label();
            _configHealthLabel.enableRichText = true;
            _configHealthLabel.style.whiteSpace = WhiteSpace.Normal;
            _configHealthLabel.style.marginTop = 6;
            _configHealthLabel.style.fontSize = 11;
            bar.Add(_configHealthLabel);

            _configChangeLabel = new Label();
            _configChangeLabel.enableRichText = true;
            _configChangeLabel.style.whiteSpace = WhiteSpace.Normal;
            _configChangeLabel.style.marginTop = 4;
            _configChangeLabel.style.fontSize = 11;
            bar.Add(_configChangeLabel);
            return bar;
        }

        private static string GetStatusRichText(string status)
        {
            if (status == "错误") return "<color=#f44336>错误</color>";
            if (status == "警告") return "<color=#ff9800>警告</color>";
            if (status == "正常") return "<color=#4caf50>正常</color>";
            return status;
        }

        private void AddConfigSourceButtons(VisualElement root)
        {
            EnsureConfigSources();
            for (int i = 0; i < _configSources.Count; i++)
            {
                IConfigEditorSource source = _configSources[i];
                bool isSelected = source.Name == _selectedConfigSourceName;
                root.Add(CreateConfigSourceButton(source, isSelected));
            }
        }

        private Button CreateConfigSourceButton(IConfigEditorSource source, bool isSelected)
        {
            var button = new Button(() =>
            {
                _selectedConfigSourceName = source.Name;
                _selectedConfigFieldName = string.Empty;
                RefreshConfigView();
            });

            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.FlexStart;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.paddingTop = 6;
            button.style.paddingBottom = 6;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.marginTop = 4;
            button.style.marginBottom = 4;
            button.style.borderTopWidth = button.style.borderRightWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = 1;

            Color defBg = isSelected ? new Color(0.15f, 0.35f, 0.6f) : new Color(0.18f, 0.18f, 0.18f);
            Color hvrBg = isSelected ? new Color(0.2f, 0.45f, 0.75f) : new Color(0.24f, 0.24f, 0.24f);
            Color borderCol = isSelected ? new Color(0.25f, 0.5f, 0.85f) : new Color(0.24f, 0.24f, 0.24f);

            button.style.backgroundColor = defBg;
            button.style.borderTopColor = button.style.borderRightColor = button.style.borderBottomColor = button.style.borderLeftColor = borderCol;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            string kindStr = source.Schema.StructureKind.ToString();
            var iconName = kindStr == "Table" ? "d_FilterSelectedOnly" : "d_CustomTool";
            var icon = CreateIcon(iconName, 12);
            if (icon != null)
                titleRow.Add(icon);

            var nameLabel = new Label(source.Name);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = Color.white;
            nameLabel.style.fontSize = 11;
            titleRow.Add(nameLabel);
            button.Add(titleRow);

            var subRow = new VisualElement();
            subRow.style.flexDirection = FlexDirection.Row;
            subRow.style.justifyContent = Justify.SpaceBetween;
            subRow.style.width = Length.Percent(100);
            subRow.style.marginTop = 2;

            var kindLabel = new Label(kindStr);
            kindLabel.style.color = isSelected ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.6f, 0.6f, 0.6f);
            kindLabel.style.fontSize = 10;
            subRow.Add(kindLabel);

            var countLabel = new Label(source.RowCount + " rows");
            countLabel.style.color = isSelected ? Color.white : new Color(0.35f, 0.65f, 0.95f);
            countLabel.style.fontSize = 10;
            countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            subRow.Add(countLabel);

            button.Add(subRow);

            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                button.style.backgroundColor = hvrBg;
                if (!isSelected)
                    button.style.borderTopColor = button.style.borderRightColor = button.style.borderBottomColor = button.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            });
            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                button.style.backgroundColor = defBg;
                if (!isSelected)
                    button.style.borderTopColor = button.style.borderRightColor = button.style.borderBottomColor = button.style.borderLeftColor = borderCol;
            });

            return button;
        }

        private void RefreshConfigView()
        {
            EnsureConfigSources();
            RefreshConfigHealth();
            IConfigEditorSource source = GetSelectedConfigSource();
            if (source == null)
                return;

            ConfigSchema schema = source.Schema;
            if (_configDetailsRoot != null)
            {
                _configDetailsRoot.Clear();
                _configDetailsRoot.Add(CreateWorkspaceTabs());
                PopulateWorkspacePage(source, schema);
            }

            RefreshConfigFieldInspector(source);

            ConfigAuthoringTemplate template = source.CreateTemplate();
            if (_configTemplatePreview != null)
                _configTemplatePreview.value = template.Text + "\npreview:\n" + source.CreateTsvPreview(5);

            _lastReport = MxEditorUtils.CreateConfigSourceReport(source);

            if (_configValidationBannerContainer != null && _configValidationBannerContainer.childCount == 0)
            {
                _configValidationBannerContainer.Clear();
                _configValidationBannerContainer.Add(CreateAlertBanner("提示", "尚未执行当前配置源校验。", false));
            }
        }

        private void RefreshConfigValidation()
        {
            IConfigEditorSource source = GetSelectedConfigSource();
            if (source == null)
                return;

            ConfigAuthoringReport report = source.Validate();
            bool valid = !report.HasErrors;
            _lastReport = source.CreateReport();

            if (_configValidationBannerContainer != null)
            {
                _configValidationBannerContainer.Clear();
                string titleText = valid ? "校验结果：正常" : "校验结果：发现问题";
                VisualElement banner = CreateAlertBanner(titleText, _lastReport, !valid);
                _configValidationBannerContainer.Add(banner);
            }

            if (_configValidationLabel != null)
            {
                _configValidationLabel.text = (valid ? "校验结果：正常\n\n" : "校验结果：发现问题\n\n") + _lastReport;
                _configValidationLabel.style.color = valid ? new Color(0.45f, 0.78f, 0.45f) : new Color(1f, 0.55f, 0.35f);
            }
        }
    }
}
