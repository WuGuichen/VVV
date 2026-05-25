using System;
using MxFramework.DebugUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Story.Editor
{
    public static class StoryEditorDebugWindowView
    {
        public const string RootName = "story-runtime-debug-root";
        public const string TitleName = "story-runtime-debug-title";
        public const string ContentName = "story-runtime-debug-content";
        public const string ReportName = "story-runtime-debug-report";

        private static readonly Color AccentColor = new Color(0.36f, 0.68f, 0.89f);
        private static readonly Color CardBackground = new Color(0.20f, 0.20f, 0.20f);
        private static readonly Color CardBorder = new Color(0.32f, 0.32f, 0.32f);
        private static readonly Color MutedText = new Color(0.62f, 0.64f, 0.68f);
        private static readonly Color PrimaryText = new Color(0.91f, 0.92f, 0.94f);
        private static readonly Color StatBackground = new Color(0.17f, 0.17f, 0.17f);

        public static void BuildReadonlyTree(VisualElement host, DebugUiDashboardViewModel dashboard, string reportText)
        {
            if (host == null)
                return;

            RemoveExistingRoot(host);
            dashboard ??= DebugUiDashboardViewModel.Empty;

            var root = new VisualElement { name = RootName };
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;

            root.Add(CreateHeader());
            root.Add(CreateInfoBanner());
            root.Add(CreateStatsRow(dashboard));

            var content = new ScrollView(ScrollViewMode.Vertical) { name = ContentName };
            content.style.flexGrow = 1;
            content.style.minHeight = 120;
            content.style.marginTop = 4;
            content.style.marginBottom = 8;
            AddDashboard(content, dashboard);
            root.Add(content);

            root.Add(CreateReportFoldout(reportText ?? string.Empty));
            host.Add(root);
        }

        private static VisualElement CreateHeader()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 10;

            var accent = new VisualElement();
            accent.style.width = 4;
            accent.style.height = 28;
            accent.style.backgroundColor = AccentColor;
            accent.style.borderTopLeftRadius = 2;
            accent.style.borderTopRightRadius = 2;
            accent.style.borderBottomLeftRadius = 2;
            accent.style.borderBottomRightRadius = 2;
            accent.style.marginRight = 10;
            row.Add(accent);

            var textColumn = new VisualElement();
            textColumn.style.flexGrow = 1;

            var title = new Label("Story 运行时调试") { name = TitleName };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            title.style.color = PrimaryText;
            title.style.marginBottom = 2;
            textColumn.Add(title);

            var subtitle = new Label("只读快照 · 通过 RuntimeCommandBuffer 写入调试命令");
            subtitle.style.fontSize = 11;
            subtitle.style.color = MutedText;
            textColumn.Add(subtitle);

            row.Add(textColumn);
            return row;
        }

        private static VisualElement CreateInfoBanner()
        {
            var banner = new VisualElement();
            ApplyInsetPanelStyle(banner, padding: 10);
            banner.style.backgroundColor = new Color(0.16f, 0.22f, 0.28f);
            banner.style.borderTopColor = new Color(0.28f, 0.42f, 0.52f);
            banner.style.borderRightColor = banner.style.borderTopColor;
            banner.style.borderBottomColor = banner.style.borderTopColor;
            banner.style.borderLeftColor = banner.style.borderTopColor;
            banner.style.marginBottom = 10;

            var label = new Label(
                "本窗口仅展示 Story Director / Runtime 模块状态。触发、选项、表演完成等操作请走显式命令入口，不要在此直接改运行时对象。");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 11;
            label.style.color = new Color(0.78f, 0.86f, 0.92f);
            banner.Add(label);
            return banner;
        }

        private static VisualElement CreateStatsRow(DebugUiDashboardViewModel dashboard)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 8;

            row.Add(CreateStatCard("数据源", dashboard.SourceCount.ToString(), "已注册调试源"));
            row.Add(CreateStatCard("采集错误", dashboard.ErrorCount.ToString(), dashboard.HasErrors ? "部分源快照失败" : "全部成功"));
            row.Add(CreateStatCard("刷新序号", dashboard.RefreshSequence.ToString(), "Debug UI 聚合版本"));
            return row;
        }

        private static VisualElement CreateStatCard(string title, string value, string hint)
        {
            var card = new VisualElement();
            card.style.flexGrow = 1;
            card.style.flexBasis = 0;
            card.style.marginRight = 8;
            ApplyInsetPanelStyle(card, padding: 10);
            card.style.backgroundColor = StatBackground;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.color = MutedText;
            titleLabel.style.marginBottom = 2;
            card.Add(titleLabel);

            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 20;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = PrimaryText;
            valueLabel.style.marginBottom = 2;
            card.Add(valueLabel);

            var hintLabel = new Label(hint);
            hintLabel.style.fontSize = 10;
            hintLabel.style.color = MutedText;
            hintLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(hintLabel);

            return card;
        }

        private static Foldout CreateReportFoldout(string reportText)
        {
            var foldout = new Foldout
            {
                text = "完整快照报告",
                value = false
            };
            foldout.style.marginTop = 4;

            var report = new TextField { name = ReportName };
            report.multiline = true;
            report.isReadOnly = true;
            report.value = reportText;
            report.style.height = 200;
            report.style.unityFontStyleAndWeight = FontStyle.Normal;
            report.style.fontSize = 11;
            report.style.whiteSpace = WhiteSpace.Normal;
            report.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            report.style.color = new Color(0.82f, 0.84f, 0.86f);
            report.style.borderTopWidth = 1;
            report.style.borderRightWidth = 1;
            report.style.borderBottomWidth = 1;
            report.style.borderLeftWidth = 1;
            report.style.borderTopColor = CardBorder;
            report.style.borderRightColor = CardBorder;
            report.style.borderBottomColor = CardBorder;
            report.style.borderLeftColor = CardBorder;
            report.style.borderTopLeftRadius = 4;
            report.style.borderTopRightRadius = 4;
            report.style.borderBottomLeftRadius = 4;
            report.style.borderBottomRightRadius = 4;
            report.style.paddingLeft = 8;
            report.style.paddingRight = 8;
            report.style.paddingTop = 6;
            report.style.paddingBottom = 6;
            foldout.Add(report);
            return foldout;
        }

        private static void AddDashboard(VisualElement root, DebugUiDashboardViewModel dashboard)
        {
            if (dashboard.HasErrors)
                root.Add(CreateErrorsPanel(dashboard));

            if (dashboard.SourceCount == 0)
            {
                root.Add(CreateEmptyStateCard());
                return;
            }

            for (int i = 0; i < dashboard.Sources.Count; i++)
                root.Add(CreateSourceCard(dashboard.Sources[i]));
        }

        private static VisualElement CreateErrorsPanel(DebugUiDashboardViewModel dashboard)
        {
            var panel = new VisualElement();
            ApplyCardStyle(panel);
            panel.style.backgroundColor = new Color(0.28f, 0.14f, 0.14f);
            panel.style.borderTopColor = new Color(0.55f, 0.22f, 0.22f);
            panel.style.borderRightColor = panel.style.borderTopColor;
            panel.style.borderBottomColor = panel.style.borderTopColor;
            panel.style.borderLeftColor = panel.style.borderTopColor;
            panel.style.marginBottom = 10;

            var title = new Label("快照采集错误");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(1f, 0.72f, 0.72f);
            title.style.marginBottom = 6;
            panel.Add(title);

            for (int i = 0; i < dashboard.Errors.Count; i++)
            {
                DebugUiErrorViewModel error = dashboard.Errors[i];
                var line = new Label("[" + error.SourceName + "] " + error.ExceptionType + ": " + error.Message);
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.fontSize = 11;
                line.style.color = new Color(0.95f, 0.82f, 0.82f);
                line.style.marginBottom = 4;
                panel.Add(line);
            }

            return panel;
        }

        private static VisualElement CreateEmptyStateCard()
        {
            var card = new VisualElement();
            ApplyCardStyle(card);

            var title = new Label("状态");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = PrimaryText;
            title.style.marginBottom = 6;
            card.Add(title);

            var body = new Label("没有已注册的 Story Runtime 调试源。请确认 Play Mode 已启动且组合根已注册 StoryRuntimeDebugSource。");
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.color = MutedText;
            body.style.fontSize = 12;
            card.Add(body);
            return card;
        }

        private static VisualElement CreateSourceCard(DebugUiSourceViewModel source)
        {
            var card = new VisualElement();
            ApplyCardStyle(card);
            card.style.marginBottom = 10;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var titleColumn = new VisualElement();
            titleColumn.style.flexGrow = 1;

            var name = new Label(source.SourceName);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.fontSize = 15;
            name.style.color = PrimaryText;
            titleColumn.Add(name);

            var meta = new Label(FormatSourceMeta(source));
            meta.style.fontSize = 11;
            meta.style.color = MutedText;
            meta.style.marginTop = 2;
            titleColumn.Add(meta);

            header.Add(titleColumn);
            header.Add(CreateStatusBadge(source));
            card.Add(header);

            if (!string.IsNullOrEmpty(source.StatusMessage))
            {
                var statusMessage = new Label(source.StatusMessage);
                statusMessage.style.whiteSpace = WhiteSpace.Normal;
                statusMessage.style.fontSize = 11;
                statusMessage.style.color = MutedText;
                statusMessage.style.marginBottom = 8;
                card.Add(statusMessage);
            }

            var sections = new VisualElement();
            sections.style.flexDirection = FlexDirection.Row;
            sections.style.flexWrap = Wrap.Wrap;

            for (int i = 0; i < source.Sections.Count; i++)
            {
                DebugUiSectionViewModel section = source.Sections[i];
                sections.Add(CreateSectionCard(section.Title, section.Body));
            }

            card.Add(sections);
            return card;
        }

        private static VisualElement CreateStatusBadge(DebugUiSourceViewModel source)
        {
            string label;
            Color background;
            Color foreground;
            switch (source.Status)
            {
                case DebugUiSourceStatus.Error:
                    label = "错误";
                    background = new Color(0.45f, 0.16f, 0.16f);
                    foreground = new Color(1f, 0.75f, 0.75f);
                    break;
                case DebugUiSourceStatus.Unavailable:
                    label = "不可用";
                    background = new Color(0.28f, 0.28f, 0.28f);
                    foreground = new Color(0.78f, 0.78f, 0.78f);
                    break;
                default:
                    label = "可用";
                    background = new Color(0.14f, 0.30f, 0.20f);
                    foreground = new Color(0.72f, 0.95f, 0.78f);
                    break;
            }

            var badge = new Label(label);
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.fontSize = 11;
            badge.style.color = foreground;
            badge.style.backgroundColor = background;
            badge.style.paddingLeft = 10;
            badge.style.paddingRight = 10;
            badge.style.paddingTop = 4;
            badge.style.paddingBottom = 4;
            badge.style.borderTopLeftRadius = 10;
            badge.style.borderTopRightRadius = 10;
            badge.style.borderBottomLeftRadius = 10;
            badge.style.borderBottomRightRadius = 10;
            return badge;
        }

        private static VisualElement CreateSectionCard(string titleText, string bodyText)
        {
            var section = new VisualElement();
            section.style.flexGrow = 1;
            section.style.flexBasis = 280;
            section.style.minWidth = 240;
            section.style.marginRight = 8;
            section.style.marginBottom = 8;
            ApplyInsetPanelStyle(section, padding: 8);
            section.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f);

            var title = new Label(titleText ?? string.Empty);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 12;
            title.style.color = AccentColor;
            title.style.marginBottom = 6;
            section.Add(title);

            AddSectionBody(section, bodyText);
            return section;
        }

        private static void AddSectionBody(VisualElement section, string bodyText)
        {
            string body = bodyText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(body))
            {
                var empty = new Label("(空)");
                empty.style.color = MutedText;
                empty.style.fontSize = 11;
                section.Add(empty);
                return;
            }

            if (TryCreateKeyValueBody(section, body))
                return;

            var mono = new Label(body);
            mono.style.whiteSpace = WhiteSpace.Normal;
            mono.style.fontSize = 11;
            mono.style.color = PrimaryText;
            section.Add(mono);
        }

        private static bool TryCreateKeyValueBody(VisualElement section, string body)
        {
            string[] lines = body.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return false;

            int structuredLines = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (TrySplitKeyValue(lines[i], out _, out _))
                    structuredLines++;
            }

            if (structuredLines < 2 || structuredLines * 2 < lines.Length)
                return false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!TrySplitKeyValue(lines[i], out string key, out string value))
                    continue;

                section.Add(CreateKeyValueRow(key, value));
            }

            return true;
        }

        private static VisualElement CreateKeyValueRow(string key, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 3;

            var keyLabel = new Label(key);
            keyLabel.style.width = 148;
            keyLabel.style.minWidth = 148;
            keyLabel.style.fontSize = 11;
            keyLabel.style.color = MutedText;
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(keyLabel);

            var valueLabel = new Label(value);
            valueLabel.style.flexGrow = 1;
            valueLabel.style.fontSize = 11;
            valueLabel.style.color = PrimaryText;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(valueLabel);
            return row;
        }

        private static bool TrySplitKeyValue(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(line))
                return false;

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                separator = line.IndexOf(':');
                if (separator <= 0)
                    return false;
            }

            key = line.Substring(0, separator).Trim();
            value = line.Substring(separator + 1).Trim();
            return key.Length > 0;
        }

        private static string FormatSourceMeta(DebugUiSourceViewModel source)
        {
            return "模式 " + source.Mode + " · " + (source.IsAvailable ? "快照可读" : "快照不可用");
        }

        private static void ApplyCardStyle(VisualElement element)
        {
            ApplyInsetPanelStyle(element, padding: 12);
            element.style.backgroundColor = CardBackground;
            element.style.borderTopColor = CardBorder;
            element.style.borderRightColor = CardBorder;
            element.style.borderBottomColor = CardBorder;
            element.style.borderLeftColor = CardBorder;
        }

        private static void ApplyInsetPanelStyle(VisualElement element, int padding)
        {
            element.style.borderTopWidth = 1;
            element.style.borderRightWidth = 1;
            element.style.borderBottomWidth = 1;
            element.style.borderLeftWidth = 1;
            element.style.borderTopLeftRadius = 6;
            element.style.borderTopRightRadius = 6;
            element.style.borderBottomLeftRadius = 6;
            element.style.borderBottomRightRadius = 6;
            element.style.paddingLeft = padding;
            element.style.paddingRight = padding;
            element.style.paddingTop = padding;
            element.style.paddingBottom = padding;
        }

        private static void RemoveExistingRoot(VisualElement host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                if (host[i].name == RootName)
                    host.RemoveAt(i);
            }
        }
    }
}
