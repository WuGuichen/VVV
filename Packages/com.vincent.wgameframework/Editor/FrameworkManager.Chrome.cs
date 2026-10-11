using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed partial class FrameworkManager
    {
        private static Image CreateIcon(string iconName, float size = 16f)
        {
            Texture2D tex = FindBuiltInTexture(iconName);
            if (tex != null)
            {
                var icon = new Image();
                icon.image = tex;
                icon.style.width = size;
                icon.style.height = size;
                icon.style.marginRight = 4;
                icon.style.alignSelf = Align.Center;
                return icon;
            }
            return null;
        }

        private static Texture2D FindBuiltInTexture(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            // Try exact name
            Texture2D tex = EditorGUIUtility.FindTexture(name);
            if (tex != null) return tex;

            // Try with d_ prefix stripped/added
            if (name.StartsWith("d_"))
            {
                tex = EditorGUIUtility.FindTexture(name.Substring(2));
                if (tex != null) return tex;
            }
            else
            {
                tex = EditorGUIUtility.FindTexture("d_" + name);
                if (tex != null) return tex;
            }

            // Fallback mappings to guarantee silence and valid visuals
            string fallbackName = null;
            if (name.Contains("UnityLogo")) fallbackName = "Settings";
            else if (name.Contains("Copy") || name.Contains("Duplicate") || name.Contains("Clipboard")) fallbackName = "TreeEditor.Duplicate";
            else if (name.Contains("Save")) fallbackName = "SaveAs";
            else if (name.Contains("Play")) fallbackName = "PlayButton";
            else if (name.Contains("Refresh")) fallbackName = "Refresh";
            else if (name.Contains("warn")) fallbackName = "console.warnicon.sml";
            else if (name.Contains("error")) fallbackName = "console.erroricon.sml";
            else if (name.Contains("Folder")) fallbackName = "Folder Icon";
            else if (name.Contains("TextAsset")) fallbackName = "TextAsset Icon";
            else if (name.Contains("Linked")) fallbackName = "TreeEditor.Duplicate";
            else if (name.Contains("Search") || name.Contains("Searchify")) fallbackName = "Settings";
            else if (name.Contains("Checkmark")) fallbackName = "console.infoicon.sml";
            else if (name.Contains("Filter") || name.Contains("Custom") || name.Contains("Profiler")) fallbackName = "Settings";

            if (fallbackName != null)
            {
                tex = EditorGUIUtility.FindTexture(fallbackName);
                if (tex != null) return tex;

                if (!fallbackName.StartsWith("d_"))
                {
                    tex = EditorGUIUtility.FindTexture("d_" + fallbackName);
                    if (tex != null) return tex;
                }
            }

            // Universal fallback
            return EditorGUIUtility.FindTexture("Settings");
        }

        private static Button CreateStyledButton(string text, string iconName, System.Action onClick, bool isSelected = false, Color? customNormalBg = null)
        {
            var button = new Button(onClick);
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.marginRight = 6;
            button.style.marginBottom = 4;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.paddingTop = 5;
            button.style.paddingBottom = 5;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;

            button.style.borderTopWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;

            Color defBg = customNormalBg ?? new Color(0.24f, 0.24f, 0.24f);
            Color hvrBg = customNormalBg != null ? new Color(customNormalBg.Value.r * 1.15f, customNormalBg.Value.g * 1.15f, customNormalBg.Value.b * 1.15f) : new Color(0.28f, 0.28f, 0.28f);
            Color borderCol = new Color(0.18f, 0.18f, 0.18f);

            if (isSelected)
            {
                defBg = new Color(0.15f, 0.35f, 0.6f);
                hvrBg = new Color(0.2f, 0.45f, 0.75f);
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
                borderCol = new Color(0.25f, 0.5f, 0.85f);
            }

            button.style.backgroundColor = defBg;
            button.style.borderTopColor = borderCol;
            button.style.borderRightColor = borderCol;
            button.style.borderBottomColor = borderCol;
            button.style.borderLeftColor = borderCol;

            if (!string.IsNullOrEmpty(iconName))
            {
                var icon = CreateIcon(iconName, 14);
                if (icon != null)
                    button.Add(icon);
            }

            var label = new Label(text);
            label.style.marginLeft = string.IsNullOrEmpty(iconName) ? 0 : 2;
            label.style.color = isSelected ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            button.Add(label);

            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                button.style.backgroundColor = hvrBg;
                if (!isSelected)
                {
                    button.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
                    button.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
                    button.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
                    button.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
                }
            });

            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                button.style.backgroundColor = defBg;
                if (!isSelected)
                {
                    button.style.borderTopColor = borderCol;
                    button.style.borderRightColor = borderCol;
                    button.style.borderBottomColor = borderCol;
                    button.style.borderLeftColor = borderCol;
                }
            });

            return button;
        }

        private static VisualElement CreateHeader()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.flexShrink = 0;
            container.style.paddingBottom = 8;
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);

            var icon = CreateIcon("d_UnityLogo", 32);
            if (icon == null || icon.image == null)
                icon = CreateIcon("d_Settings", 32);
            container.Add(icon);

            var textContainer = new VisualElement();
            textContainer.style.flexDirection = FlexDirection.Column;
            textContainer.style.marginLeft = 8;

            var title = new Label("MxFramework 框架管理器");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            title.style.color = new Color(0.92f, 0.92f, 0.92f);

            var subtitle = new Label("查看编辑配置、运行调试入口、文档和基础验证状态。");
            subtitle.style.color = new Color(0.6f, 0.6f, 0.6f);
            subtitle.style.fontSize = 11;
            subtitle.style.marginTop = 2;

            textContainer.Add(title);
            textContainer.Add(subtitle);
            container.Add(textContainer);
            return container;
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginTop = 10;
            toolbar.style.marginBottom = 6;
            toolbar.style.flexShrink = 0;

            toolbar.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.paddingTop = 6;
            toolbar.style.paddingBottom = 6;
            toolbar.style.borderTopLeftRadius = 4;
            toolbar.style.borderTopRightRadius = 4;
            toolbar.style.borderBottomLeftRadius = 4;
            toolbar.style.borderBottomRightRadius = 4;

            var btnAuthoring = CreateStyledButton("编辑模式", "d_Settings", () => SetDisplayMode(EditorDisplayMode.Authoring), _displayMode == EditorDisplayMode.Authoring);
            toolbar.Add(btnAuthoring);

            var btnRuntime = CreateStyledButton("运行模式", "d_PlayButton", () => SetDisplayMode(EditorDisplayMode.Runtime), _displayMode == EditorDisplayMode.Runtime);
            toolbar.Add(btnRuntime);

            var separator = new VisualElement();
            separator.style.width = 1;
            separator.style.height = 16;
            separator.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f);
            separator.style.marginRight = 8;
            separator.style.marginLeft = 4;
            toolbar.Add(separator);

            toolbar.Add(CreateStyledButton("使用手册", "d_TextAsset Icon", () => MxEditorUtils.OpenProjectDocument("Docs/USAGE.md")));
            toolbar.Add(CreateStyledButton("设计文档", "d_TextAsset Icon", () => MxEditorUtils.OpenProjectDocument("Docs/DESIGN.md")));
            toolbar.Add(CreateStyledButton("接口文档", "d_TextAsset Icon", () => MxEditorUtils.OpenProjectDocument("Docs/INTERFACES.md")));

            var separator2 = new VisualElement();
            separator2.style.width = 1;
            separator2.style.height = 16;
            separator2.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f);
            separator2.style.marginRight = 8;
            separator2.style.marginLeft = 4;
            toolbar.Add(separator2);

            toolbar.Add(CreateStyledButton("执行验证", "d_Checkmark", RefreshValidation));
            toolbar.Add(CreateStyledButton("复制报告", "d_Copy", CopyLastReport));

            return toolbar;
        }

        private VisualElement CreateAuthoringTabs()
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

            var btnModules = CreatePillTabButton("模块概览", "d_FilterSelectedOnly", () => SetAuthoringPage(AuthoringPage.Modules), _authoringPage == AuthoringPage.Modules);
            tabs.Add(btnModules);

            var btnConfig = CreatePillTabButton("配置工作台", "d_CustomTool", () => SetAuthoringPage(AuthoringPage.Config), _authoringPage == AuthoringPage.Config);
            tabs.Add(btnConfig);

            var btnTools = CreatePillTabButton("工具入口", "d_Profiler", () => SetAuthoringPage(AuthoringPage.Tools), _authoringPage == AuthoringPage.Tools);
            tabs.Add(btnTools);

            return tabs;
        }

        private static Button CreatePillTabButton(string text, string iconName, System.Action onClick, bool isSelected)
        {
            var button = new Button(onClick);
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.paddingLeft = 14;
            button.style.paddingRight = 14;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 4;
            button.style.borderTopLeftRadius = 11;
            button.style.borderTopRightRadius = 11;
            button.style.borderBottomLeftRadius = 11;
            button.style.borderBottomRightRadius = 11;
            button.style.borderTopWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.marginLeft = 2;
            button.style.marginRight = 2;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;

            Color defBg = isSelected ? new Color(0.2f, 0.42f, 0.72f) : Color.clear;
            Color hvrBg = isSelected ? new Color(0.24f, 0.48f, 0.8f) : new Color(0.18f, 0.18f, 0.18f);

            button.style.backgroundColor = defBg;
            button.style.color = isSelected ? Color.white : new Color(0.75f, 0.75f, 0.75f);
            if (isSelected)
                button.style.unityFontStyleAndWeight = FontStyle.Bold;

            if (!string.IsNullOrEmpty(iconName))
            {
                var icon = CreateIcon(iconName, 13);
                if (icon != null)
                    button.Add(icon);
            }

            var label = new Label(text);
            label.style.marginLeft = string.IsNullOrEmpty(iconName) ? 0 : 2;
            label.style.color = isSelected ? Color.white : new Color(0.75f, 0.75f, 0.75f);
            button.Add(label);

            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                button.style.backgroundColor = hvrBg;
                if (!isSelected)
                    label.style.color = Color.white;
            });
            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                button.style.backgroundColor = defBg;
                if (!isSelected)
                    label.style.color = new Color(0.75f, 0.75f, 0.75f);
            });

            return button;
        }

        private static VisualElement CreatePanel(string titleText)
        {
            var panel = new VisualElement();
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 4;

            panel.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);

            Color accentColor = new Color(0.23f, 0.51f, 0.96f);
            if (titleText.Contains("模块") || titleText.Contains("健康") || titleText.Contains("概览") || titleText.Contains("数据源"))
                accentColor = new Color(0.23f, 0.65f, 0.96f);
            else if (titleText.Contains("校验") || titleText.Contains("验证") || titleText.Contains("结果") || titleText.Contains("运行"))
                accentColor = new Color(0.3f, 0.69f, 0.31f);
            else if (titleText.Contains("问题") || titleText.Contains("错误"))
                accentColor = new Color(0.96f, 0.26f, 0.21f);
            else if (titleText.Contains("工具"))
                accentColor = new Color(0.61f, 0.34f, 0.82f);

            panel.style.borderLeftColor = accentColor;
            panel.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f, 1f);

            panel.style.borderTopLeftRadius = 0;
            panel.style.borderBottomLeftRadius = 0;
            panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomRightRadius = 6;

            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;
            panel.style.marginBottom = 8;

            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Row;
            titleContainer.style.alignItems = Align.Center;
            titleContainer.style.borderBottomWidth = 1;
            titleContainer.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);
            titleContainer.style.paddingBottom = 6;
            titleContainer.style.marginBottom = 8;

            string iconName = "d_Settings";
            if (titleText.Contains("模块")) iconName = "d_FilterSelectedOnly";
            else if (titleText.Contains("验证") || titleText.Contains("结果")) iconName = "d_Checkmark";
            else if (titleText.Contains("运行")) iconName = "d_PlayButton";
            else if (titleText.Contains("工具")) iconName = "d_Profiler";
            else if (titleText.Contains("配置") || titleText.Contains("工作")) iconName = "d_CustomTool";
            else if (titleText.Contains("问题") || titleText.Contains("错误")) iconName = "d_console.warnicon.sml";
            else if (titleText.Contains("字段")) iconName = "d_Searchify";

            var icon = CreateIcon(iconName, 14);
            if (icon != null)
                titleContainer.Add(icon);

            var title = new Label(titleText);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 12;
            title.style.color = new Color(0.9f, 0.9f, 0.9f);
            title.style.marginLeft = 2;
            titleContainer.Add(title);

            panel.Add(titleContainer);
            return panel;
        }

        private static Label CreateCellPercent(string text, float percentWidth, bool isHeader)
        {
            var label = new Label(text);
            label.style.width = Length.Percent(percentWidth);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = 4;
            label.style.paddingRight = 4;
            label.style.fontSize = 11;
            if (isHeader)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = new Color(0.95f, 0.95f, 0.95f);
            }
            else
            {
                label.style.color = new Color(0.82f, 0.82f, 0.82f);
            }
            return label;
        }

        private static VisualElement CreateModuleRow(string module, string assembly, string deps, string status, bool isHeader)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = 26;
            row.style.alignItems = Align.Center;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;

            if (isHeader)
            {
                row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                row.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);
                row.style.borderBottomWidth = 2;
            }

            row.Add(CreateCellPercent(module, 20, isHeader));
            row.Add(CreateCellPercent(assembly, 30, isHeader));
            row.Add(CreateCellPercent(deps, 38, isHeader));
            row.Add(CreateCellPercent(status, 12, isHeader));
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
            row.style.minHeight = 26;
            row.style.alignItems = Align.Center;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;

            if (isHeader)
            {
                row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                row.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);
                row.style.borderBottomWidth = 2;
            }

            float descPercent = (onClick != null && !isHeader) ? 17f : 25f;

            row.Add(CreateCellPercent(name, 22, isHeader));
            row.Add(CreateCellPercent(type, 20, isHeader));
            row.Add(CreateCellPercent(required, 10, isHeader));
            row.Add(CreateCellPercent(reference, 23, isHeader));
            row.Add(CreateCellPercent(description, descPercent, isHeader));

            if (!isHeader && onClick != null)
            {
                var btn = CreateStyledButton("详情", "d_Searchify", onClick);
                btn.style.width = 54;
                btn.style.height = 20;
                btn.style.paddingLeft = 2;
                btn.style.paddingRight = 2;
                btn.style.paddingTop = 1;
                btn.style.paddingBottom = 1;
                btn.style.marginRight = 0;
                btn.style.marginBottom = 0;
                btn.style.alignSelf = Align.Center;
                row.Add(btn);
            }
            return row;
        }

        private static Label CreateCell(string text, int width, bool isHeader)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = 4;
            label.style.paddingRight = 4;
            label.style.fontSize = 11;
            if (isHeader)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = new Color(0.95f, 0.95f, 0.95f);
            }
            else
            {
                label.style.color = new Color(0.82f, 0.82f, 0.82f);
            }
            return label;
        }

        private static Button CreateButton(string text, System.Action onClick)
        {
            return CreateStyledButton(text, null, onClick);
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

        private static VisualElement CreateAlertBanner(string title, string content, bool isError)
        {
            var banner = new VisualElement();
            banner.style.paddingLeft = 12;
            banner.style.paddingRight = 12;
            banner.style.paddingTop = 10;
            banner.style.paddingBottom = 10;
            banner.style.borderLeftWidth = 4;
            banner.style.borderTopRightRadius = 6;
            banner.style.borderBottomRightRadius = 6;
            banner.style.marginTop = 8;
            banner.style.marginBottom = 8;

            Color leftBarColor = isError ? new Color(0.96f, 0.26f, 0.21f, 1f) : new Color(0.3f, 0.69f, 0.31f, 1f);
            Color bgColor = isError ? new Color(0.96f, 0.26f, 0.21f, 0.08f) : new Color(0.3f, 0.69f, 0.31f, 0.08f);

            banner.style.borderLeftColor = leftBarColor;
            banner.style.backgroundColor = bgColor;

            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Row;
            titleContainer.style.alignItems = Align.Center;

            var iconName = isError ? "d_console.erroricon.sml" : "d_Checkmark";
            var icon = CreateIcon(iconName, 14);
            if (icon != null)
                titleContainer.Add(icon);

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.color = leftBarColor;
            titleLabel.style.marginLeft = 2;
            titleContainer.Add(titleLabel);
            banner.Add(titleContainer);

            var textLabel = new Label(content);
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            textLabel.style.marginTop = 6;
            textLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            textLabel.style.fontSize = 11;
            banner.Add(textLabel);

            return banner;
        }

        private void CopyLastReport()
        {
            if (string.IsNullOrEmpty(_lastReport))
                RefreshValidation();

            EditorGUIUtility.systemCopyBuffer = _lastReport ?? string.Empty;
            ShowNotification(new GUIContent("报告已复制"));
        }
    }
}
