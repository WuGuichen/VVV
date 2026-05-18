using System;
using System.Collections.Generic;
using MxFramework.DebugUI;
using MxFramework.UI.Toolkit;
using UnityEngine.UIElements;

namespace MxFramework.DebugUI.Toolkit
{
    public sealed class DebugUiOverlayViewModelBinder
    {
        private readonly string[] _tabs = { "Overview", "Snapshots", "Logs" };
        private VisualElement _root;
        private VisualElement _collapsed;
        private VisualElement _expanded;
        private Label _collapsedSummary;
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

        public VisualElement Root => _root;
        public int ActiveTab => _activeTab;

        public void Build(VisualElement host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            host.Clear();

            _root = new VisualElement { name = DebugUiToolkitThemeTokens.RootName };
            _root.AddToClassList(DebugUiToolkitThemeTokens.Root);
            host.Add(_root);

            _collapsed = new VisualElement { name = DebugUiToolkitThemeTokens.CollapsedName };
            _collapsed.AddToClassList(DebugUiToolkitThemeTokens.Collapsed);
            _collapsedSummary = new Label("-");
            _collapsed.Add(_collapsedSummary);
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
            toolbar.Add(refresh);
            toolbar.Add(pause);
            toolbar.Add(collapse);
            toolbar.Add(close);

            _panelTabs = new MxPanelTabs();
            _panelTabs.SetTabs(_tabs, _activeTab);
            _panelTabs.TabSelected += index =>
            {
                _activeTab = index;
                RenderContent(_currentModel);
            };
            _expanded.Add(_panelTabs);

            _content = new ScrollView { name = DebugUiToolkitThemeTokens.ContentName };
            _content.AddToClassList(DebugUiToolkitThemeTokens.SourceListName);
            _expanded.Add(_content);
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

        private static VisualElement CreateCard(DebugUiSourceViewModel source)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList(DebugUiToolkitThemeTokens.SourceCard);
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
            return label;
        }

        private static Label CreateBody(string value)
        {
            return new Label(string.IsNullOrEmpty(value) ? "-" : value);
        }

        private static string FormatCollapsedSummary(DebugUiDashboardViewModel model, bool refreshPaused)
        {
            return "Debug UI sources=" + model.SourceCount
                + " errors=" + model.ErrorCount
                + (refreshPaused ? " paused" : " live");
        }

        private void EnsureBuilt()
        {
            if (_root == null)
                throw new InvalidOperationException("Debug UI overlay view has not been built.");
        }
    }
}
