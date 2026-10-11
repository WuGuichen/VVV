using System;
using System.Collections.Generic;
using MxFramework.DebugUI;
using MxFramework.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.DebugUI.Toolkit
{
    public sealed class DebugUiOverlayViewModelBinder
    {
        private readonly string[] _tabs = { "Overview", "Snapshots", "Timeline", "Entities", "Logs" };
        private VisualElement _root;
        private VisualElement _collapsed;
        private VisualElement _expanded;
        private Label _collapsedSummary;
        private MxCommandButton _collapsedExpandButton;
        private Label _title;
        private MxStatusBadge _sourceBadge;
        private MxStatusBadge _errorBadge;
        private MxStatusBadge _pauseBadge;
        private MxPanelTabs _panelTabs;
        private ScrollView _content;
        private DebugUiDashboardViewModel _currentModel = DebugUiDashboardViewModel.Empty;
        private int _activeTab;

        public event Action RefreshRequested;
        public event Action PauseToggled;
        public event Action CloseRequested;
        public event Action CollapseRequested;
        public event Action ExpandRequested;

        public VisualElement Root => _root;
        public int ActiveTab => _activeTab;

        public void SetActiveTab(int index)
        {
            _activeTab = Math.Max(0, Math.Min(index, _tabs.Length - 1));
            if (_panelTabs != null)
                _panelTabs.SetTabs(_tabs, _activeTab);
            RenderContent(_currentModel);
        }

        public void Build(VisualElement host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            RemoveExistingRoot(host);

            _root = new VisualElement { name = DebugUiToolkitThemeTokens.RootName };
            _root.AddToClassList(DebugUiToolkitThemeTokens.Root);
            host.Add(_root);

            _collapsed = new VisualElement { name = DebugUiToolkitThemeTokens.CollapsedName };
            _collapsed.AddToClassList(DebugUiToolkitThemeTokens.Collapsed);
            _collapsedSummary = new Label("-");
            _collapsed.Add(_collapsedSummary);
            _collapsedExpandButton = new MxCommandButton(() => ExpandRequested?.Invoke(), "Expand")
            {
                name = DebugUiToolkitThemeTokens.ExpandButtonName
            };
            _collapsedExpandButton.SetState(true, true, "Expand dashboard");
            StyleCommandButton(_collapsedExpandButton, hot: true);
            _collapsed.Add(_collapsedExpandButton);
            _root.Add(_collapsed);

            _expanded = new VisualElement { name = DebugUiToolkitThemeTokens.ExpandedName };
            _expanded.AddToClassList(DebugUiToolkitThemeTokens.Expanded);
            _root.Add(_expanded);

            VisualElement header = new VisualElement { name = DebugUiToolkitThemeTokens.HeaderName };
            header.AddToClassList(DebugUiToolkitThemeTokens.Header);
            _expanded.Add(header);

            _title = new Label("Debug UI");
            header.Add(_title);

            _sourceBadge = new MxStatusBadge("0 sources", MxUiTone.Neutral);
            _errorBadge = new MxStatusBadge("0 errors", MxUiTone.Positive);
            _pauseBadge = new MxStatusBadge("Live", MxUiTone.Positive);
            header.Add(_sourceBadge);
            header.Add(_errorBadge);
            header.Add(_pauseBadge);

            VisualElement toolbar = new VisualElement { name = DebugUiToolkitThemeTokens.ToolbarName };
            toolbar.AddToClassList(DebugUiToolkitThemeTokens.Toolbar);
            _expanded.Add(toolbar);

            var refresh = new MxCommandButton(() => RefreshRequested?.Invoke(), "Refresh");
            refresh.SetState(true, false, "Refresh dashboard");
            var pause = new MxCommandButton(() => PauseToggled?.Invoke(), "Pause");
            pause.SetState(true, false, "Pause or resume refresh");
            var collapse = new MxCommandButton(() => CollapseRequested?.Invoke(), "Collapse");
            collapse.SetState(true, false, "Collapse dashboard");
            var close = new MxCommandButton(() => CloseRequested?.Invoke(), "Close");
            close.SetState(true, false, "Hide dashboard");
            StyleCommandButton(refresh);
            StyleCommandButton(pause);
            StyleCommandButton(collapse);
            StyleCommandButton(close);
            toolbar.Add(refresh);
            toolbar.Add(pause);
            toolbar.Add(collapse);
            toolbar.Add(close);

            _panelTabs = new MxPanelTabs();
            _panelTabs.SetTabs(_tabs, _activeTab);
            _panelTabs.TabSelected += index =>
            {
                _activeTab = index;
                StyleTabs();
                RenderContent(_currentModel);
            };
            _expanded.Add(_panelTabs);

            _content = new ScrollView { name = DebugUiToolkitThemeTokens.ContentName };
            _content.AddToClassList(DebugUiToolkitThemeTokens.SourceListName);
            _expanded.Add(_content);
            ApplyInlineStyles();
        }

        public void Bind(DebugUiDashboardViewModel model, DebugUiVisibility visibility, bool refreshPaused)
        {
            EnsureBuilt();
            model = model ?? DebugUiDashboardViewModel.Empty;
            _currentModel = model;

            _root.EnableInClassList(DebugUiToolkitThemeTokens.RootHidden, visibility == DebugUiVisibility.Hidden);
            _root.EnableInClassList(DebugUiToolkitThemeTokens.RootCollapsed, visibility == DebugUiVisibility.Collapsed);
            _root.EnableInClassList(DebugUiToolkitThemeTokens.RootExpanded, visibility == DebugUiVisibility.Expanded);

            _root.style.display = visibility == DebugUiVisibility.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
            _collapsed.style.display = visibility == DebugUiVisibility.Collapsed ? DisplayStyle.Flex : DisplayStyle.None;
            _expanded.style.display = visibility == DebugUiVisibility.Expanded ? DisplayStyle.Flex : DisplayStyle.None;

            _collapsedSummary.text = FormatCollapsedSummary(model, refreshPaused);
            _title.text = "Debug UI #" + model.RefreshSequence;
            _sourceBadge.Set(model.SourceCount + " sources", model.SourceCount == 0 ? MxUiTone.Warning : MxUiTone.Neutral);
            _errorBadge.Set(model.ErrorCount + " errors", model.ErrorCount == 0 ? MxUiTone.Positive : MxUiTone.Danger);
            _pauseBadge.Set(refreshPaused ? "Paused" : "Live", refreshPaused ? MxUiTone.Warning : MxUiTone.Positive);
            StyleStatusBadge(_sourceBadge, model.SourceCount == 0 ? MxUiTone.Warning : MxUiTone.Neutral);
            StyleStatusBadge(_errorBadge, model.ErrorCount == 0 ? MxUiTone.Positive : MxUiTone.Danger);
            StyleStatusBadge(_pauseBadge, refreshPaused ? MxUiTone.Warning : MxUiTone.Positive);

            RenderContent(model);
        }

        private void RenderContent(DebugUiDashboardViewModel model)
        {
            if (_content == null)
                return;

            _content.Clear();
            model = model ?? DebugUiDashboardViewModel.Empty;

            if (_activeTab == 0)
            {
                RenderOverview(model);
                return;
            }

            if (_activeTab == 2)
            {
                RenderSectionsByTitle(model, "Timeline", DebugUiToolkitThemeTokens.EmptyText);
                return;
            }

            if (_activeTab == 3)
            {
                RenderSectionsByTitle(model, "Entity Watch", DebugUiToolkitThemeTokens.EmptyText);
                return;
            }

            if (_activeTab == 4)
            {
                RenderLogs(model);
                return;
            }

            RenderSnapshots(model.Sources);
        }

        private void RenderOverview(DebugUiDashboardViewModel model)
        {
            if (model.SourceCount == 0)
            {
                _content.Add(CreateBody(DebugUiToolkitThemeTokens.EmptyText));
                return;
            }

            _content.Add(CreateBody("sources=" + model.SourceCount + " errors=" + model.ErrorCount));
            for (int i = 0; i < model.Sources.Count; i++)
            {
                DebugUiSourceViewModel source = model.Sources[i];
                _content.Add(CreateSourceHeader(source));
            }

            for (int i = 0; i < model.Errors.Count; i++)
            {
                DebugUiErrorViewModel error = model.Errors[i];
                var label = CreateBody(error.SourceName + ": " + error.ExceptionType + " " + error.Message);
                label.AddToClassList(DebugUiToolkitThemeTokens.ErrorText);
                _content.Add(label);
            }
        }

        private void RenderSnapshots(IReadOnlyList<DebugUiSourceViewModel> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                _content.Add(CreateBody(DebugUiToolkitThemeTokens.EmptyText));
                return;
            }

            for (int i = 0; i < sources.Count; i++)
            {
                DebugUiSourceViewModel source = sources[i];
                VisualElement card = CreateCard(source);
                if (source.Sections.Count == 0)
                    card.Add(CreateBody("none"));

                for (int j = 0; j < source.Sections.Count; j++)
                {
                    DebugUiSectionViewModel section = source.Sections[j];
                    Label title = CreateBody(section.Title);
                    title.AddToClassList(DebugUiToolkitThemeTokens.SectionTitle);
                    card.Add(title);

                    Label body = CreateBody(section.IsEmpty ? "empty" : section.Body);
                    body.AddToClassList(DebugUiToolkitThemeTokens.SectionBody);
                    card.Add(body);
                }

                _content.Add(card);
            }
        }

        private void RenderLogs(DebugUiDashboardViewModel model)
        {
            var lines = new List<string>();
            for (int i = 0; i < model.Sources.Count; i++)
            {
                DebugUiSourceViewModel source = model.Sources[i];
                for (int j = 0; j < source.Sections.Count; j++)
                {
                    DebugUiSectionViewModel section = source.Sections[j];
                    if (string.Equals(section.Title, "Logs", StringComparison.OrdinalIgnoreCase)
                        || source.SourceName.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        lines.Add(source.SourceName + " / " + section.Title);
                        if (!string.IsNullOrEmpty(section.Body))
                            lines.Add(section.Body);
                    }
                }
            }

            var log = new MxEventLog();
            log.SetItems(lines, "No logs", newestFirst: false);
            _content.Add(log);
        }

        private void RenderSectionsByTitle(
            DebugUiDashboardViewModel model,
            string title,
            string emptyText)
        {
            bool added = false;
            for (int i = 0; i < model.Sources.Count; i++)
            {
                DebugUiSourceViewModel source = model.Sources[i];
                for (int j = 0; j < source.Sections.Count; j++)
                {
                    DebugUiSectionViewModel section = source.Sections[j];
                    if (!string.Equals(section.Title, title, StringComparison.OrdinalIgnoreCase))
                        continue;

                    VisualElement card = CreateCard(source);
                    Label body = CreateBody(section.IsEmpty ? "empty" : section.Body);
                    body.AddToClassList(DebugUiToolkitThemeTokens.SectionBody);
                    card.Add(body);
                    _content.Add(card);
                    added = true;
                }
            }

            if (!added)
                _content.Add(CreateBody(emptyText));
        }

        private static VisualElement CreateCard(DebugUiSourceViewModel source)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList(DebugUiToolkitThemeTokens.SourceCard);
            StyleCard(card);
            card.Add(CreateSourceHeader(source));
            return card;
        }

        private static Label CreateSourceHeader(DebugUiSourceViewModel source)
        {
            string text = source.SourceName + " [" + source.Mode + "] " + source.Status;
            if (!string.IsNullOrEmpty(source.StatusMessage))
                text += ": " + source.StatusMessage;

            Label label = CreateBody(text);
            label.AddToClassList(DebugUiToolkitThemeTokens.SourceTitle);
            label.style.color = source.Status == DebugUiSourceStatus.Available
                ? ColorFromRgb(0xD8, 0xEF, 0xFF)
                : ColorFromRgb(0xFF, 0xC7, 0x62);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Label CreateBody(string value)
        {
            var label = new Label(string.IsNullOrEmpty(value) ? "-" : value);
            label.style.color = ColorFromRgb(0xC6, 0xD3, 0xE1);
            label.style.fontSize = 12;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            return label;
        }

        private void ApplyInlineStyles()
        {
            StyleRoot(_root);
            StyleCollapsed(_collapsed, _collapsedSummary);
            StyleExpanded(_expanded);
            StyleHeader(_title);
            StyleToolbar();
            StyleTabs();
            StyleContent();
            StyleStatusBadge(_sourceBadge, MxUiTone.Neutral);
            StyleStatusBadge(_errorBadge, MxUiTone.Positive);
            StyleStatusBadge(_pauseBadge, MxUiTone.Positive);
        }

        private static void StyleRoot(VisualElement root)
        {
            root.style.width = 560;
            root.style.maxWidth = 560;
            root.style.maxHeight = 650;
            root.style.backgroundColor = ColorFromRgba(0x08, 0x12, 0x20, 0xEE);
            root.style.borderTopLeftRadius = 8;
            root.style.borderTopRightRadius = 8;
            root.style.borderBottomLeftRadius = 8;
            root.style.borderBottomRightRadius = 8;
            root.style.borderLeftColor = ColorFromRgb(0x1F, 0xD6, 0xE8);
            root.style.borderRightColor = ColorFromRgb(0x1F, 0xD6, 0xE8);
            root.style.borderTopColor = ColorFromRgb(0x1F, 0xD6, 0xE8);
            root.style.borderBottomColor = ColorFromRgb(0x1F, 0xD6, 0xE8);
            root.style.borderLeftWidth = 1;
            root.style.borderRightWidth = 1;
            root.style.borderTopWidth = 1;
            root.style.borderBottomWidth = 1;
        }

        private static void StyleCollapsed(VisualElement collapsed, Label summary)
        {
            collapsed.style.flexDirection = FlexDirection.Row;
            collapsed.style.alignItems = Align.Center;
            collapsed.style.justifyContent = Justify.SpaceBetween;
            collapsed.style.paddingLeft = 12;
            collapsed.style.paddingRight = 8;
            collapsed.style.paddingTop = 8;
            collapsed.style.paddingBottom = 8;
            summary.style.color = ColorFromRgb(0xE6, 0xF2, 0xFF);
            summary.style.fontSize = 12;
        }

        private static void StyleExpanded(VisualElement expanded)
        {
            expanded.style.paddingLeft = 10;
            expanded.style.paddingRight = 10;
            expanded.style.paddingTop = 10;
            expanded.style.paddingBottom = 10;
        }

        private static void StyleHeader(Label title)
        {
            VisualElement header = title.parent;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            title.style.color = ColorFromRgb(0xF2, 0xFA, 0xFF);
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private void StyleToolbar()
        {
            _root.Q<VisualElement>(DebugUiToolkitThemeTokens.ToolbarName).style.flexDirection = FlexDirection.Row;
            _root.Q<VisualElement>(DebugUiToolkitThemeTokens.ToolbarName).style.marginTop = 8;
        }

        private void StyleTabs()
        {
            if (_panelTabs == null)
                return;

            _panelTabs.style.flexDirection = FlexDirection.Row;
            _panelTabs.style.marginTop = 8;
            for (int i = 0; i < _panelTabs.childCount; i++)
            {
                if (!(_panelTabs[i] is Button tab))
                    continue;

                bool active = i == _activeTab;
                tab.style.flexGrow = 1;
                tab.style.height = 28;
                tab.style.paddingLeft = 4;
                tab.style.paddingRight = 4;
                tab.style.paddingTop = 0;
                tab.style.paddingBottom = 0;
                tab.style.marginLeft = 0;
                tab.style.marginRight = 4;
                tab.style.marginTop = 0;
                tab.style.marginBottom = 0;
                tab.style.backgroundColor = active
                    ? ColorFromRgb(0x10, 0x4B, 0x5E)
                    : ColorFromRgb(0x12, 0x1C, 0x2D);
                tab.style.color = active
                    ? ColorFromRgb(0xF3, 0xFC, 0xFF)
                    : ColorFromRgb(0xA8, 0xB7, 0xC7);
                tab.style.borderLeftWidth = 1;
                tab.style.borderRightWidth = 1;
                tab.style.borderTopWidth = 1;
                tab.style.borderBottomWidth = 1;
                tab.style.borderLeftColor = active ? ColorFromRgb(0x20, 0xD9, 0xEE) : ColorFromRgb(0x25, 0x36, 0x4D);
                tab.style.borderRightColor = active ? ColorFromRgb(0x20, 0xD9, 0xEE) : ColorFromRgb(0x25, 0x36, 0x4D);
                tab.style.borderTopColor = active ? ColorFromRgb(0x20, 0xD9, 0xEE) : ColorFromRgb(0x25, 0x36, 0x4D);
                tab.style.borderBottomColor = active ? ColorFromRgb(0x20, 0xD9, 0xEE) : ColorFromRgb(0x25, 0x36, 0x4D);
                tab.style.borderTopLeftRadius = 5;
                tab.style.borderTopRightRadius = 5;
                tab.style.borderBottomLeftRadius = 5;
                tab.style.borderBottomRightRadius = 5;
                tab.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void StyleContent()
        {
            _content.style.height = 500;
            _content.style.maxHeight = 500;
            _content.style.backgroundColor = ColorFromRgba(0x03, 0x09, 0x13, 0xD8);
            _content.style.borderTopLeftRadius = 6;
            _content.style.borderTopRightRadius = 6;
            _content.style.borderBottomLeftRadius = 6;
            _content.style.borderBottomRightRadius = 6;
            _content.style.paddingLeft = 8;
            _content.style.paddingRight = 8;
            _content.style.paddingTop = 8;
            _content.style.paddingBottom = 8;
        }

        private static void StyleCard(VisualElement card)
        {
            card.style.backgroundColor = ColorFromRgba(0x0D, 0x19, 0x2A, 0xF2);
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderLeftColor = ColorFromRgb(0x23, 0x3A, 0x55);
            card.style.borderRightColor = ColorFromRgb(0x23, 0x3A, 0x55);
            card.style.borderTopColor = ColorFromRgb(0x23, 0x3A, 0x55);
            card.style.borderBottomColor = ColorFromRgb(0x23, 0x3A, 0x55);
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.marginBottom = 8;
        }

        private static void StyleCommandButton(Button button, bool hot = false)
        {
            button.style.height = 28;
            button.style.minWidth = 72;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.backgroundColor = hot ? ColorFromRgb(0x0D, 0x57, 0x66) : ColorFromRgb(0x14, 0x22, 0x35);
            button.style.color = ColorFromRgb(0xE7, 0xF8, 0xFF);
            button.style.borderLeftColor = ColorFromRgb(0x2A, 0xC8, 0xDD);
            button.style.borderRightColor = ColorFromRgb(0x2A, 0xC8, 0xDD);
            button.style.borderTopColor = ColorFromRgb(0x2A, 0xC8, 0xDD);
            button.style.borderBottomColor = ColorFromRgb(0x2A, 0xC8, 0xDD);
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
        }

        private static void StyleStatusBadge(MxStatusBadge badge, MxUiTone tone)
        {
            badge.style.height = 22;
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.marginLeft = 0;
            badge.style.marginRight = 0;
            badge.style.marginTop = 0;
            badge.style.marginBottom = 0;
            badge.style.borderTopLeftRadius = 999;
            badge.style.borderTopRightRadius = 999;
            badge.style.borderBottomLeftRadius = 999;
            badge.style.borderBottomRightRadius = 999;
            badge.style.fontSize = 11;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;

            Color color;
            Color background;
            if (tone == MxUiTone.Positive)
            {
                color = ColorFromRgb(0x99, 0xF6, 0xBF);
                background = ColorFromRgba(0x12, 0x4C, 0x34, 0xFF);
            }
            else if (tone == MxUiTone.Warning)
            {
                color = ColorFromRgb(0xFE, 0xD7, 0x8B);
                background = ColorFromRgba(0x62, 0x3D, 0x10, 0xFF);
            }
            else if (tone == MxUiTone.Danger)
            {
                color = ColorFromRgb(0xFF, 0xB4, 0xB4);
                background = ColorFromRgba(0x61, 0x1E, 0x2C, 0xFF);
            }
            else
            {
                color = ColorFromRgb(0xCB, 0xD9, 0xEA);
                background = ColorFromRgba(0x1A, 0x2A, 0x3D, 0xFF);
            }

            badge.style.color = color;
            badge.style.backgroundColor = background;
        }

        private static Color ColorFromRgb(byte r, byte g, byte b)
        {
            return ColorFromRgba(r, g, b, 0xFF);
        }

        private static Color ColorFromRgba(byte r, byte g, byte b, byte a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private static string FormatCollapsedSummary(DebugUiDashboardViewModel model, bool refreshPaused)
        {
            return "Debug UI sources=" + model.SourceCount
                + " errors=" + model.ErrorCount
                + (refreshPaused ? " paused" : " live");
        }

        private static void RemoveExistingRoot(VisualElement host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                VisualElement child = host[i];
                if (string.Equals(child.name, DebugUiToolkitThemeTokens.RootName, StringComparison.Ordinal))
                    child.RemoveFromHierarchy();
            }
        }

        private void EnsureBuilt()
        {
            if (_root == null)
                throw new InvalidOperationException("Debug UI overlay view has not been built.");
        }
    }
}
