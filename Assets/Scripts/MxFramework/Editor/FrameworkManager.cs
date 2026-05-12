using System.Collections.Generic;
using MxFramework.Config;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed class FrameworkManager : EditorWindow
    {
        private readonly List<VisualElement> _moduleRows = new List<VisualElement>();
        private IReadOnlyList<IConfigEditorSource> _configSources;
        private IReadOnlyList<ConfigIssueView> _configIssues;
        private ConfigHealthReport _configHealth;
        private ConfigChangeReport _configChangeReport;
        private EditorDisplayMode _displayMode;
        private AuthoringPage _authoringPage;
        private ConfigWorkspacePage _configWorkspacePage;
        private VisualElement _contentRoot;
        private VisualElement _configDetailsRoot;
        private Label _validationLabel;
        private Label _runtimeLabel;
        private Label _configHealthLabel;
        private Label _configChangeLabel;
        private Label _configValidationLabel;
        private VisualElement _configFieldInspectorRoot;
        private TextField _configIssuePreview;
        private TextField _configTemplatePreview;
        private PopupField<string> _configIssueFilterPopup;
        private string _selectedConfigSourceName;
        private string _selectedConfigFieldName;
        private string _lastReport;

        [MenuItem("MxFramework/Framework Manager")]
        public static void Open()
        {
            FrameworkManager window = GetWindow<FrameworkManager>();
            window.titleContent = new GUIContent("MxFramework");
            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        public void CreateGUI()
        {
            _moduleRows.Clear();
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            root.Add(CreateHeader());
            root.Add(CreateToolbar());

            _contentRoot = new ScrollView(ScrollViewMode.Vertical);
            _contentRoot.style.flexGrow = 1;
            _contentRoot.contentContainer.style.flexGrow = 1;
            _contentRoot.style.minHeight = 0;
            _contentRoot.style.marginTop = 10;
            root.Add(_contentRoot);

            RebuildContent();
        }

        private static VisualElement CreateHeader()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.flexShrink = 0;

            var title = new Label("MxFramework 框架管理器");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            title.style.marginBottom = 4;

            var subtitle = new Label("查看编辑配置、运行调试入口、文档和基础验证状态。");
            subtitle.style.color = new Color(0.55f, 0.55f, 0.55f);

            // container.Add(title);
            // container.Add(subtitle);
            return container;
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.marginTop = 10;
            toolbar.style.marginBottom = 10;
            toolbar.style.flexShrink = 0;

            var btnAuthoring = CreateButton("编辑模式", () => SetDisplayMode(EditorDisplayMode.Authoring));
            if (_displayMode == EditorDisplayMode.Authoring)
            {
                btnAuthoring.style.unityFontStyleAndWeight = FontStyle.Bold;
                btnAuthoring.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f, 0.8f);
            }
            toolbar.Add(btnAuthoring);

            var btnRuntime = CreateButton("运行模式", () => SetDisplayMode(EditorDisplayMode.Runtime));
            if (_displayMode == EditorDisplayMode.Runtime)
            {
                btnRuntime.style.unityFontStyleAndWeight = FontStyle.Bold;
                btnRuntime.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f, 0.8f);
            }
            toolbar.Add(btnRuntime);

            var separator = new VisualElement();
            separator.style.width = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            separator.style.marginRight = 6;
            separator.style.marginLeft = 2;
            separator.style.marginTop = 4;
            separator.style.marginBottom = 8;
            toolbar.Add(separator);

            toolbar.Add(CreateButton("打开使用手册", () => MxEditorUtils.OpenProjectDocument("Docs/USAGE.md")));
            toolbar.Add(CreateButton("打开设计文档", () => MxEditorUtils.OpenProjectDocument("Docs/DESIGN.md")));
            toolbar.Add(CreateButton("打开接口文档", () => MxEditorUtils.OpenProjectDocument("Docs/INTERFACES.md")));

            var separator2 = new VisualElement();
            separator2.style.width = 1;
            separator2.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            separator2.style.marginRight = 6;
            separator2.style.marginLeft = 2;
            separator2.style.marginTop = 4;
            separator2.style.marginBottom = 8;
            toolbar.Add(separator2);

            toolbar.Add(CreateButton("执行验证", RefreshValidation));
            toolbar.Add(CreateButton("复制报告", CopyLastReport));

            return toolbar;
        }

        private void RebuildContent()
        {
            if (_contentRoot == null)
                return;

            _contentRoot.Clear();
            if (_displayMode == EditorDisplayMode.Runtime)
            {
                _contentRoot.Add(CreateRuntimePanel());
                RefreshRuntimeView();
                return;
            }

            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.flexGrow = 1;

            var authoring = new VisualElement();
            authoring.style.flexDirection = FlexDirection.Column;
            authoring.style.flexGrow = 1;

            authoring.Add(CreateAuthoringTabs());
            if (_authoringPage == AuthoringPage.Config)
            {
                authoring.Add(CreateConfigPanel());
            }
            else
            {
                content.Add(CreateModulePanel());
                content.Add(CreateValidationPanel());
                authoring.Add(content);
            }

            _contentRoot.Add(authoring);

            if (_authoringPage == AuthoringPage.Config)
                RefreshConfigView();
            else
                RefreshValidation();
        }

        private VisualElement CreateModulePanel()
        {
            var panel = CreatePanel("模块列表");
            panel.style.flexGrow = 1;
            panel.style.marginRight = 8;
            panel.style.minHeight = 300;

            VisualElement header = CreateModuleRow("模块", "程序集", "依赖", "状态", isHeader: true);
            panel.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            IReadOnlyList<FrameworkModuleInfo> modules = MxEditorUtils.GetModules();
            for (int i = 0; i < modules.Count; i++)
            {
                FrameworkModuleInfo module = modules[i];
                VisualElement row = CreateModuleRow(module.Name, module.AssemblyName, module.Dependencies, module.Status, isHeader: false);
                _moduleRows.Add(row);
                scroll.Add(row);
            }
            panel.Add(scroll);

            return panel;
        }

        private VisualElement CreateRuntimePanel()
        {
            var panel = CreatePanel("运行模式");
            panel.style.flexGrow = 1;
            panel.style.minHeight = 300;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            _runtimeLabel = new Label();
            _runtimeLabel.style.whiteSpace = WhiteSpace.Normal;
            _runtimeLabel.style.marginTop = 6;
            scroll.Add(_runtimeLabel);

            var note = new Label("运行模式默认只读。后续游戏层通过 IFrameworkDebugSource 提供 Attribute、Buff、Modifier、AI 等快照。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 12;
            note.style.color = new Color(0.55f, 0.55f, 0.55f);
            scroll.Add(note);

            panel.Add(scroll);
            return panel;
        }

        private VisualElement CreateAuthoringTabs()
        {
            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexWrap = Wrap.Wrap;
            tabs.style.marginBottom = 8;
            tabs.style.flexShrink = 0;

            var btnModules = CreateButton("模块概览", () => SetAuthoringPage(AuthoringPage.Modules));
            if (_authoringPage == AuthoringPage.Modules)
            {
                btnModules.style.unityFontStyleAndWeight = FontStyle.Bold;
                btnModules.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f, 0.8f);
            }
            tabs.Add(btnModules);

            var btnConfig = CreateButton("配置工作台", () => SetAuthoringPage(AuthoringPage.Config));
            if (_authoringPage == AuthoringPage.Config)
            {
                btnConfig.style.unityFontStyleAndWeight = FontStyle.Bold;
                btnConfig.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f, 0.8f);
            }
            tabs.Add(btnConfig);

            return tabs;
        }

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
            sourceNote.style.color = new Color(0.55f, 0.55f, 0.55f);
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
                CreateButton("复制模板", CopyConfigTemplate),
                CreateButton("复制问题", CopyConfigIssueList),
                CreateButton("健康报告", CopyConfigHealthReport)));
            reportScroll.Add(CreateButtonRow(
                CreateButton("变动报告", CopyConfigChangeReport),
                CreateButton("重置基线", ResetConfigChangeBaseline)));

            var templateTitle = new Label("源预览");
            templateTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            templateTitle.style.marginTop = 4;
            templateTitle.style.marginBottom = 4;
            reportScroll.Add(templateTitle);

            _configTemplatePreview = new TextField();
            _configTemplatePreview.multiline = true;
            _configTemplatePreview.isReadOnly = true;
            _configTemplatePreview.style.height = 120;
            _configTemplatePreview.style.marginTop = 0;
            reportScroll.Add(_configTemplatePreview);

            _configValidationLabel = new Label();
            _configValidationLabel.style.whiteSpace = WhiteSpace.Normal;
            _configValidationLabel.style.marginTop = 10;
            reportScroll.Add(_configValidationLabel);

            var fieldTitle = new Label("字段详情");
            fieldTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            fieldTitle.style.marginTop = 10;
            fieldTitle.style.marginBottom = 4;
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
            header.Add(_configIssueFilterPopup);

            var note = new Label("全局配置问题在这里集中查看，主工作区保持聚焦。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.color = new Color(0.55f, 0.55f, 0.55f);
            note.style.marginLeft = 8;
            header.Add(note);
            drawer.Add(header);

            _configIssuePreview = new TextField();
            _configIssuePreview.multiline = true;
            _configIssuePreview.isReadOnly = true;
            _configIssuePreview.style.flexGrow = 1;
            _configIssuePreview.style.marginTop = 6;
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
            string summary = sourceName + "    " + structure + "    " + status;
            var label = new Label(summary);
            label.style.minWidth = 280;
            label.style.marginRight = 8;
            label.style.whiteSpace = WhiteSpace.Normal;
            row.Add(label);

            row.Add(CreateButton("刷新", RefreshConfigSources));
            row.Add(CreateButton("校验", RefreshConfigValidation));
            row.Add(CreateButton("提交前检查", RunConfigPrecommitCheck));
            row.Add(CreateButton("AI 上下文", CopyConfigAiFixContext));
            row.Add(CreateButton("导出报告", ExportConfigReportBundle));
            row.Add(CreateButton("更多报告", CopyConfigHealthReport));

            bar.Add(row);

            _configHealthLabel = new Label();
            _configHealthLabel.style.whiteSpace = WhiteSpace.Normal;
            _configHealthLabel.style.marginTop = 6;
            bar.Add(_configHealthLabel);

            _configChangeLabel = new Label();
            _configChangeLabel.style.whiteSpace = WhiteSpace.Normal;
            _configChangeLabel.style.marginTop = 4;
            bar.Add(_configChangeLabel);
            return bar;
        }

        private void AddConfigSourceButtons(VisualElement root)
        {
            EnsureConfigSources();
            for (int i = 0; i < _configSources.Count; i++)
            {
                IConfigEditorSource source = _configSources[i];
                var button = new Button(() =>
                {
                    _selectedConfigSourceName = source.Name;
                    _selectedConfigFieldName = string.Empty;
                    RefreshConfigView();
                });
                button.text = source.Schema.StructureKind + "  " + source.Name + "\n" + source.RowCount + " rows";
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginTop = 4;
                button.style.whiteSpace = WhiteSpace.Normal;

                if (source.Name == _selectedConfigSourceName)
                {
                    button.style.unityFontStyleAndWeight = FontStyle.Bold;
                    button.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f, 0.8f);
                }

                root.Add(button);
            }
        }

        private VisualElement CreateValidationPanel()
        {
            var panel = CreatePanel("验证结果");
            panel.style.width = 300;
            panel.style.flexShrink = 0;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            _validationLabel = new Label();
            _validationLabel.style.whiteSpace = WhiteSpace.Normal;
            _validationLabel.style.marginTop = 6;
            scroll.Add(_validationLabel);

            var note = new Label("Phase 8.0 只验证模块 asmdef 是否存在。依赖图、GitNexus 和沙盒检查会在后续阶段接入。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 12;
            note.style.color = new Color(0.55f, 0.55f, 0.55f);
            scroll.Add(note);

            panel.Add(scroll);
            return panel;
        }

        private static VisualElement CreatePanel(string titleText)
        {
            var panel = new VisualElement();
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new Color(0.24f, 0.24f, 0.24f);
            panel.style.borderRightColor = new Color(0.24f, 0.24f, 0.24f);
            panel.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);
            panel.style.borderLeftColor = new Color(0.24f, 0.24f, 0.24f);

            panel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f);
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;

            panel.style.paddingLeft = 8;
            panel.style.paddingRight = 8;
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;

            var title = new Label(titleText);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;

            title.style.borderBottomWidth = 1;
            title.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);
            title.style.paddingBottom = 4;

            panel.Add(title);
            return panel;
        }

        private static VisualElement CreateModuleRow(string module, string assembly, string deps, string status, bool isHeader)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = 24;
            row.style.alignItems = Align.Center;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f);

            row.Add(CreateCell(module, 120, isHeader));
            row.Add(CreateCell(assembly, 200, isHeader));
            row.Add(CreateCell(deps, 240, isHeader));
            row.Add(CreateCell(status, 80, isHeader));
            return row;
        }

        private static VisualElement CreateConfigFieldRow(
            string name,
            string type,
            string required,
            string reference,
            string description,
            bool isHeader,
            System.Action onClick = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = 24;
            row.style.alignItems = Align.Center;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f);

            row.Add(CreateCell(name, 120, isHeader));
            row.Add(CreateCell(type, 120, isHeader));
            row.Add(CreateCell(required, 48, isHeader));
            row.Add(CreateCell(reference, 140, isHeader));
            row.Add(CreateCell(description, 180, isHeader));
            if (!isHeader && onClick != null)
                row.Add(CreateButton("详情", onClick));
            return row;
        }

        private static Label CreateCell(string text, int width, bool isHeader)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (isHeader)
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Button CreateButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.marginRight = 6;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 4;
            return button;
        }

        private static VisualElement CreateButtonRow(params Button[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 2;
            for (int i = 0; i < buttons.Length; i++)
                row.Add(buttons[i]);
            return row;
        }

        private void RefreshValidation()
        {
            if (_displayMode == EditorDisplayMode.Runtime)
            {
                RefreshRuntimeView();
                return;
            }

            IReadOnlyList<FrameworkModuleInfo> modules = MxEditorUtils.GetModules();
            bool valid = MxEditorUtils.ValidateModuleAssets(modules, out string report);
            _lastReport = MxEditorUtils.CreateFrameworkManagerReport(EditorDisplayMode.Authoring.ToString(), report);
            if (_validationLabel != null)
            {
                _validationLabel.text = (valid ? "模块资源：正常\n\n" : "模块资源：发现问题\n\n") + report;
                _validationLabel.style.color = valid ? new Color(0.45f, 0.78f, 0.45f) : new Color(1f, 0.55f, 0.35f);
            }
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
            if (_configValidationLabel != null && string.IsNullOrEmpty(_configValidationLabel.text))
                _configValidationLabel.text = "尚未执行当前配置源校验。";
        }

        private void RefreshConfigValidation()
        {
            IConfigEditorSource source = GetSelectedConfigSource();
            if (source == null)
                return;

            ConfigAuthoringReport report = source.Validate();
            bool valid = !report.HasErrors;
            _lastReport = source.CreateReport();
            if (_configValidationLabel != null)
            {
                _configValidationLabel.text = (valid ? "校验结果：正常\n\n" : "校验结果：发现问题\n\n") + _lastReport;
                _configValidationLabel.style.color = valid ? new Color(0.45f, 0.78f, 0.45f) : new Color(1f, 0.55f, 0.35f);
            }
        }

        private VisualElement CreateWorkspaceTabs()
        {
            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexWrap = Wrap.Wrap;
            tabs.style.marginBottom = 8;

            tabs.Add(CreateWorkspaceTabButton("概览", ConfigWorkspacePage.Overview));
            tabs.Add(CreateWorkspaceTabButton("字段", ConfigWorkspacePage.Fields));
            tabs.Add(CreateWorkspaceTabButton("行视图", ConfigWorkspacePage.Rows));
            tabs.Add(CreateWorkspaceTabButton("引用", ConfigWorkspacePage.References));
            return tabs;
        }

        private Button CreateWorkspaceTabButton(string text, ConfigWorkspacePage page)
        {
            var button = CreateButton(text, () =>
            {
                _configWorkspacePage = page;
                RefreshConfigView();
            });
            if (_configWorkspacePage == page)
            {
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
                button.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f, 0.8f);
            }
            return button;
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

        private void AddOverviewPage(IConfigEditorSource source, ConfigSchema schema)
        {
            string idRange = schema.IdRange.IsValid
                ? schema.IdRange.MinInclusive + " - " + schema.IdRange.MaxInclusive
                : "未限制";

            var summary = new Label(
                "数据源：" + source.Name +
                "\n来源类型：" + source.SourceType +
                "\n结构类型：" + schema.StructureKind +
                "\n表名：" + schema.TableName +
                "\n行数：" + source.RowCount +
                "\n字段数：" + schema.Fields.Count +
                "\n引用规则：" + schema.ReferenceRules.Count +
                "\n显示名：" + schema.DisplayName +
                "\nID 范围：" + idRange +
                "\n说明：" + schema.Description +
                "\n编辑状态：只读预览，后续 Table Editor / Graph Inspector 复用当前 Schema 和 SourceIndex。");
            summary.style.whiteSpace = WhiteSpace.Normal;
            _configDetailsRoot.Add(summary);

            if (schema.RequiredLocales.Count > 0)
            {
                var locales = new Label("必需语言：" + JoinLocales(schema.RequiredLocales));
                locales.style.marginTop = 8;
                _configDetailsRoot.Add(locales);
            }
        }

        private void AddFieldsPage(ConfigSchema schema)
        {
            _configDetailsRoot.Add(CreateConfigFieldRow("字段", "类型", "必填", "引用", "说明", isHeader: true));
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                string fieldName = field.Name;
                string reference = field.ReferenceRule.IsValid ? field.ReferenceRule.GetTargetDisplayName() : "-";
                _configDetailsRoot.Add(CreateConfigFieldRow(
                    CreateFieldLabel(field),
                    string.IsNullOrEmpty(field.EnumId) ? field.FieldType.ToString() : field.FieldType + "\n" + field.EnumId,
                    field.Required ? "是" : "否",
                    reference,
                    string.IsNullOrEmpty(field.Description) ? "-" : field.Description,
                    isHeader: false,
                    onClick: () => SelectConfigField(fieldName)));
            }
        }

        private void AddRowsPage(IConfigEditorSource source)
        {
            var title = new Label("行视图与控件映射");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            _configDetailsRoot.Add(title);
            _configDetailsRoot.Add(CreateRowPreview(source, 5));
        }

        private void AddReferencesPage(ConfigSchema schema)
        {
            var references = new Label(CreateReferenceSummary(schema));
            references.style.whiteSpace = WhiteSpace.Normal;
            _configDetailsRoot.Add(references);
        }

        private void SelectConfigField(string fieldName)
        {
            _selectedConfigFieldName = fieldName ?? string.Empty;
            IConfigEditorSource source = GetSelectedConfigSource();
            if (source != null)
                RefreshConfigFieldInspector(source);
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

            AddInspectorText(CreateFieldInspectorText(source, field));
            if (field.ReferenceRule.IsValid)
                _configFieldInspectorRoot.Add(CreateButton("查看目标源", () => JumpToReferenceTarget(field.ReferenceRule)));
        }

        private void AddInspectorText(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
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

        private void RefreshRuntimeView()
        {
            string body;
            if (!EditorApplication.isPlaying)
            {
                body = "当前不在 Play Mode。\n\n运行模式需要进入 Play Mode，或由游戏层注册 Runtime Debug Source 后才能显示真实快照。";
            }
            else
            {
                body = "当前处于 Play Mode，但还没有连接 IFrameworkDebugSource。\n\n后续阶段会提供 Debug Source 注册入口，用于显示运行时 Attribute、Buff、Modifier、Counter 和 AI 快照。";
            }

            var snapshot = new FrameworkDebugSnapshot(
                "FrameworkManager",
                FrameworkDebugMode.Runtime,
                new[] { new FrameworkDebugSection("运行模式", body) });
            _lastReport = FrameworkDebugReportExporter.ExportText(snapshot);

            if (_runtimeLabel != null)
                _runtimeLabel.text = body;
        }

        private void SetDisplayMode(EditorDisplayMode mode)
        {
            if (_displayMode == mode)
                return;

            _displayMode = mode;
            RebuildContent();
        }

        private void SetAuthoringPage(AuthoringPage page)
        {
            if (_authoringPage == page)
                return;

            _authoringPage = page;
            RebuildContent();
        }

        private void CopyLastReport()
        {
            if (string.IsNullOrEmpty(_lastReport))
                RefreshValidation();

            EditorGUIUtility.systemCopyBuffer = _lastReport ?? string.Empty;
            ShowNotification(new GUIContent("报告已复制"));
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

        private static VisualElement CreateRowPreview(IConfigEditorSource source, int maxRows)
        {
            var container = new VisualElement();
            if (!(source is IConfigEditorTablePreviewProvider provider))
            {
                var missing = new Label("当前配置源未提供行视图预览。");
                missing.style.whiteSpace = WhiteSpace.Normal;
                container.Add(missing);
                return container;
            }

            IReadOnlyList<ConfigEditorRowPreview> rows = provider.CreateRowPreview(maxRows);
            if (rows.Count == 0)
            {
                var empty = new Label("当前配置源没有可预览行。");
                empty.style.whiteSpace = WhiteSpace.Normal;
                container.Add(empty);
                return container;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                ConfigEditorRowPreview row = rows[i];
                var rowTitle = new Label("Row " + (row.RowId > 0 ? row.RowId.ToString() : (i + 1).ToString()));
                rowTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                rowTitle.style.marginTop = i == 0 ? 0 : 6;
                container.Add(rowTitle);

                for (int j = 0; j < row.Cells.Count; j++)
                {
                    ConfigEditorCellPreview cell = row.Cells[j];
                    container.Add(CreateReadonlyCellControl(cell));
                }
            }

            var note = new Label("当前为只读控件映射预览，保存功能将在 Table Editor v1 后续阶段开启。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 8;
            note.style.color = new Color(0.55f, 0.55f, 0.55f);
            container.Add(note);
            return container;
        }

        private static VisualElement CreateReadonlyCellControl(ConfigEditorCellPreview cell)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            var label = new Label(CreateCellFieldLabel(cell));
            label.style.width = 120;
            label.style.whiteSpace = WhiteSpace.Normal;
            row.Add(label);

            VisualElement control = CreateReadonlyControl(cell);
            control.SetEnabled(false);
            control.style.flexGrow = 1;
            row.Add(control);

            var hint = new Label(cell.ControlHint);
            hint.style.width = 92;
            hint.style.marginLeft = 6;
            hint.style.color = new Color(0.55f, 0.55f, 0.55f);
            row.Add(hint);

            return row;
        }

        private static string CreateFieldLabel(ConfigField field)
        {
            if (string.IsNullOrEmpty(field.DisplayName))
                return field.Name;

            return field.DisplayName + "\n" + field.Name;
        }

        private static string CreateReferenceFieldLabel(ConfigSchema schema, string fieldName)
        {
            if (schema == null)
                return fieldName;

            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                if (field.Name == fieldName && !string.IsNullOrEmpty(field.DisplayName))
                    return field.DisplayName + " " + field.Name;
            }

            return fieldName;
        }

        private static string CreateCellFieldLabel(ConfigEditorCellPreview cell)
        {
            if (string.IsNullOrEmpty(cell.FieldDisplayName))
                return cell.FieldName;

            return cell.FieldDisplayName + "\n" + cell.FieldName;
        }

        private static VisualElement CreateReadonlyControl(ConfigEditorCellPreview cell)
        {
            string value = cell.Value ?? string.Empty;
            switch (cell.ControlHint)
            {
                case "开关":
                    bool enabled = value == "True" || value == "true" || value == "1";
                    return new Toggle { value = enabled };
                case "枚举/Flags":
                case "枚举下拉":
                    return CreateReadonlyOptionsControl(cell);
                case "引用选择器":
                    return new Button { text = string.IsNullOrEmpty(value) ? "选择引用" : value };
                case "资源路径":
                    return new Button { text = string.IsNullOrEmpty(value) ? "选择资源" : value };
                default:
                    return new TextField { value = string.IsNullOrEmpty(cell.DisplayValue) ? value : cell.DisplayValue };
            }
        }

        private static VisualElement CreateReadonlyOptionsControl(ConfigEditorCellPreview cell)
        {
            var options = new List<string>();
            string selected = string.IsNullOrEmpty(cell.DisplayValue) ? cell.Value : cell.DisplayValue;
            if (!string.IsNullOrEmpty(selected))
                options.Add(selected);

            for (int i = 0; i < cell.Options.Count; i++)
            {
                if (!options.Contains(cell.Options[i]))
                    options.Add(cell.Options[i]);
            }

            if (options.Count == 0)
                options.Add("-");

            return new PopupField<string>(options, 0);
        }

        private static string CreateConfigChangeSummaryText(ConfigChangeReport report)
        {
            if (report == null || !report.HasBaseline)
                return "配置变动：已建立初始基线";

            if (!report.HasChanges)
                return "配置变动：无变化";

            return "配置变动：发现变化" +
                "\n新增源：" + report.AddedSourceCount +
                "\n移除源：" + report.RemovedSourceCount +
                "\n变化源：" + report.ChangedSourceCount +
                "\n错误类型变化：" + report.IssueTypeChangeCount;
        }

        private enum EditorDisplayMode
        {
            Authoring,
            Runtime
        }

        private enum AuthoringPage
        {
            Modules,
            Config
        }

        private enum ConfigWorkspacePage
        {
            Overview,
            Fields,
            Rows,
            References
        }
    }
}
