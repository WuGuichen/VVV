using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal readonly struct TriggerTestScenarioPickerItem
    {
        internal TriggerTestScenarioPickerItem(
            string scenarioId,
            string displayName,
            string groupName,
            TriggerTestExecutionProfile profile,
            bool canRun,
            string unavailableReason)
        {
            ScenarioId = scenarioId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            GroupName = groupName ?? string.Empty;
            Profile = profile;
            CanRun = canRun;
            UnavailableReason = unavailableReason ?? string.Empty;
        }

        internal string ScenarioId { get; }
        internal string DisplayName { get; }
        internal string GroupName { get; }
        internal TriggerTestExecutionProfile Profile { get; }
        internal bool CanRun { get; }
        internal string UnavailableReason { get; }
    }

    /// <summary>
    /// Searchable, grouped selector for the manual Trigger catalog.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. Rebuilds only after explicit
    /// Editor interaction, discovery refresh or Play Mode state changes.
    /// </remarks>
    internal sealed class TriggerTestScenarioPicker : VisualElement
    {
        private const int MaxRecentItems = 5;
        private static readonly string[] GroupOrder =
        {
            "环境准备",
            "测试控制",
            "时间轴观察",
            "实验设置",
            "其他"
        };

        private readonly List<TriggerTestScenarioPickerItem> _items =
            new List<TriggerTestScenarioPickerItem>();
        private readonly List<string> _recentScenarioIds =
            new List<string>();
        private readonly List<string> _keyboardScenarioIds =
            new List<string>();
        private readonly Button _selectionButton;
        private readonly Label _selectedName;
        private readonly Label _selectedProfile;
        private readonly Label _chevron;
        private readonly VisualElement _panel;
        private readonly ToolbarSearchField _search;
        private readonly VisualElement _filterRow;
        private readonly ScrollView _list;
        private readonly Button[] _filterButtons;
        private VisualElement _overlayRoot;
        private bool _overlayStylesAttached;
        private string _selectedId = string.Empty;
        private string _keyboardScenarioId = string.Empty;
        private TriggerTestScenarioPickerFilter _filter;
        private bool _isOpen;

        internal TriggerTestScenarioPicker()
        {
            name = "trigger-test-scenario-selector";
            focusable = true;
            AddToClassList("trigger-test-scenario-picker");
            RegisterCallback<KeyDownEvent>(
                OnKeyDown,
                TrickleDown.TrickleDown);
            RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);

            _selectionButton = new Button(Toggle)
            {
                name = "trigger-test-scenario-selector-button",
                tooltip = "打开 Trigger 选择器"
            };
            _selectionButton.AddToClassList(
                "trigger-test-scenario-picker-button");
            _selectedName = new Label("没有可用 Trigger")
            {
                name = "trigger-test-scenario-selected-name"
            };
            _selectedName.AddToClassList(
                "trigger-test-scenario-selected-name");
            _selectionButton.Add(_selectedName);
            _selectedProfile = new Label
            {
                name = "trigger-test-scenario-selected-profile"
            };
            _selectedProfile.AddToClassList(
                "trigger-test-scenario-badge");
            _selectionButton.Add(_selectedProfile);
            _chevron = new Label("▾");
            _chevron.AddToClassList(
                "trigger-test-scenario-picker-chevron");
            _selectionButton.Add(_chevron);
            Add(_selectionButton);

            _panel = new VisualElement
            {
                name = "trigger-test-scenario-picker-panel"
            };
            _panel.AddToClassList("trigger-test-scenario-picker-panel");
            _panel.style.display = DisplayStyle.None;
            _panel.RegisterCallback<KeyDownEvent>(
                OnKeyDown,
                TrickleDown.TrickleDown);
            Add(_panel);

            _search = new ToolbarSearchField
            {
                name = "trigger-test-scenario-search",
                tooltip = "按名称、分组或 ScenarioId 搜索"
            };
            _search.AddToClassList("trigger-test-scenario-search");
            _search.RegisterValueChangedCallback(_ => RebuildList());
            _panel.Add(_search);
            TextField searchText = _search.Q<TextField>();
            if (searchText != null)
                searchText.textEdition.placeholder = "搜索 Trigger…";

            _filterRow = new VisualElement
            {
                name = "trigger-test-scenario-filters"
            };
            _filterRow.AddToClassList("trigger-test-scenario-filters");
            _filterButtons = new[]
            {
                BuildFilterButton(
                    "全部",
                    TriggerTestScenarioPickerFilter.All),
                BuildFilterButton(
                    "可执行",
                    TriggerTestScenarioPickerFilter.Available),
                BuildFilterButton(
                    "Edit",
                    TriggerTestScenarioPickerFilter.EditMode),
                BuildFilterButton(
                    "Play",
                    TriggerTestScenarioPickerFilter.PlayMode)
            };
            for (int i = 0; i < _filterButtons.Length; i++)
                _filterRow.Add(_filterButtons[i]);
            _panel.Add(_filterRow);

            _list = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "trigger-test-scenario-list"
            };
            _list.AddToClassList("trigger-test-scenario-list");
            _panel.Add(_list);

            var footer = new Label("↑↓ 选择   Enter 确认   Esc 关闭")
            {
                name = "trigger-test-scenario-picker-footer"
            };
            footer.AddToClassList(
                "trigger-test-scenario-picker-footer");
            _panel.Add(footer);
            RefreshFilterClasses();
        }

        internal event Action<string> SelectionChanged;

        internal string Value => _selectedId;

        internal bool IsOpen => _isOpen;

        internal int VisibleOptionCount => _keyboardScenarioIds.Count;

        internal void SetItems(
            IReadOnlyList<TriggerTestScenarioPickerItem> items,
            string selectedId)
        {
            _items.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                    _items.Add(items[i]);
            }

            RemoveMissingRecentItems();
            if (!ContainsScenario(selectedId))
            {
                selectedId = _items.Count == 0
                    ? string.Empty
                    : _items[0].ScenarioId;
            }

            _selectedId = selectedId ?? string.Empty;
            _keyboardScenarioId = _selectedId;
            RefreshSelectionButton();
            RebuildList();
        }

        internal void SetValueWithoutNotify(string scenarioId)
        {
            if (!ContainsScenario(scenarioId))
                return;

            _selectedId = scenarioId;
            _keyboardScenarioId = scenarioId;
            RefreshSelectionButton();
            RebuildList();
        }

        internal void Open()
        {
            if (_isOpen)
                return;

            _isOpen = true;
            AttachPanelToOverlay();
            _panel.style.display = DisplayStyle.Flex;
            _chevron.text = "▴";
            _selectionButton.EnableInClassList("is-open", true);
            RebuildList();
            PositionPanel();
            schedule.Execute(() => _search.Focus());
        }

        internal void Close()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            _panel.style.display = DisplayStyle.None;
            _chevron.text = "▾";
            _selectionButton.EnableInClassList("is-open", false);
            UnregisterOverlayCallbacks();
        }

        internal void SetSearchForTests(string search)
        {
            _search.SetValueWithoutNotify(search ?? string.Empty);
            RebuildList();
        }

        internal void SetFilterForTests(string filter)
        {
            if (!Enum.TryParse(
                    filter,
                    ignoreCase: true,
                    out TriggerTestScenarioPickerFilter parsed))
            {
                parsed = TriggerTestScenarioPickerFilter.All;
            }

            SetFilter(parsed);
        }

        internal void SelectForTests(string scenarioId) =>
            Select(scenarioId);

        private Button BuildFilterButton(
            string text,
            TriggerTestScenarioPickerFilter filter)
        {
            var button = new Button(() => SetFilter(filter))
            {
                name =
                    "trigger-test-scenario-filter-" +
                    filter.ToString().ToLowerInvariant(),
                text = text
            };
            button.AddToClassList(
                "trigger-test-scenario-filter-button");
            return button;
        }

        private void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        private void AttachPanelToOverlay()
        {
            VisualElement root = panel?.visualTree;
            if (root == null)
                return;

            AttachOverlayStyleSheets();
            if (_panel.parent != root)
                root.Add(_panel);

            _overlayRoot = root;
            _overlayRoot.RegisterCallback<PointerDownEvent>(
                OnOverlayPointerDown,
                TrickleDown.TrickleDown);
            _overlayRoot.RegisterCallback<GeometryChangedEvent>(
                OnOverlayGeometryChanged);
            _panel.BringToFront();
        }

        private void AttachOverlayStyleSheets()
        {
            if (_overlayStylesAttached)
                return;

            for (VisualElement ancestor = this;
                 ancestor != null;
                 ancestor = ancestor.parent)
            {
                for (int i = 0; i < ancestor.styleSheets.count; i++)
                {
                    StyleSheet styleSheet = ancestor.styleSheets[i];
                    if (styleSheet != null)
                        _panel.styleSheets.Add(styleSheet);
                }
            }

            _overlayStylesAttached = true;
        }

        private void PositionPanel()
        {
            if (!_isOpen || _overlayRoot == null)
                return;

            Rect rootBounds = _overlayRoot.worldBound;
            Rect anchorBounds = _selectionButton.worldBound;
            if (rootBounds.width <= 0f ||
                rootBounds.height <= 0f ||
                anchorBounds.width <= 0f)
            {
                return;
            }

            const float edge = 8f;
            const float preferredWidth = 520f;
            const float preferredHeight = 536f;
            float width = Mathf.Min(
                preferredWidth,
                Mathf.Max(0f, rootBounds.width - edge * 2f));
            float left = Mathf.Clamp(
                anchorBounds.xMin - rootBounds.xMin,
                edge,
                Mathf.Max(edge, rootBounds.width - width - edge));
            float belowTop =
                anchorBounds.yMax - rootBounds.yMin + 3f;
            float availableBelow = Mathf.Max(
                0f,
                rootBounds.height - belowTop - edge);
            float availableAbove = Mathf.Max(
                0f,
                anchorBounds.yMin - rootBounds.yMin - edge - 3f);
            bool placeBelow =
                availableBelow >= 220f ||
                availableBelow >= availableAbove;
            float height = Mathf.Min(
                preferredHeight,
                placeBelow ? availableBelow : availableAbove);
            float top = belowTop;
            if (!placeBelow)
            {
                top = Mathf.Max(
                    edge,
                    anchorBounds.yMin -
                    rootBounds.yMin -
                    height - 3f);
            }

            _panel.style.left = left;
            _panel.style.right = StyleKeyword.Auto;
            _panel.style.top = top;
            _panel.style.width = width;
            _panel.style.maxHeight = height;
            _list.style.maxHeight = Mathf.Max(70f, height - 106f);
        }

        private void OnOverlayPointerDown(PointerDownEvent evt)
        {
            Vector2 position = new Vector2(
                evt.position.x,
                evt.position.y);
            if (!_panel.worldBound.Contains(position) &&
                !_selectionButton.worldBound.Contains(position))
            {
                Close();
            }
        }

        private void OnOverlayGeometryChanged(GeometryChangedEvent evt) =>
            PositionPanel();

        private void UnregisterOverlayCallbacks()
        {
            if (_overlayRoot == null)
                return;

            _overlayRoot.UnregisterCallback<PointerDownEvent>(
                OnOverlayPointerDown,
                TrickleDown.TrickleDown);
            _overlayRoot.UnregisterCallback<GeometryChangedEvent>(
                OnOverlayGeometryChanged);
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            if (!ReferenceEquals(evt.target, this))
                return;

            UnregisterOverlayCallbacks();
            _panel.RemoveFromHierarchy();
            _overlayRoot = null;
            _isOpen = false;
        }

        private void SetFilter(TriggerTestScenarioPickerFilter filter)
        {
            if (_filter == filter)
                return;

            _filter = filter;
            RefreshFilterClasses();
            RebuildList();
        }

        private void RefreshFilterClasses()
        {
            for (int i = 0; i < _filterButtons.Length; i++)
            {
                _filterButtons[i].EnableInClassList(
                    "is-selected",
                    i == (int)_filter);
            }
        }

        private void RebuildList()
        {
            _list.Clear();
            _keyboardScenarioIds.Clear();

            string query = (_search.value ?? string.Empty).Trim();
            if (_filter == TriggerTestScenarioPickerFilter.All &&
                query.Length == 0)
            {
                BuildRecentSection();
            }

            for (int groupIndex = 0;
                 groupIndex < GroupOrder.Length;
                 groupIndex++)
            {
                BuildGroupSection(GroupOrder[groupIndex], query);
            }

            if (_keyboardScenarioIds.Count == 0)
            {
                var empty = new Label("没有匹配的 Trigger")
                {
                    name = "trigger-test-scenario-picker-empty"
                };
                empty.AddToClassList(
                    "trigger-test-scenario-picker-empty");
                _list.Add(empty);
                _keyboardScenarioId = string.Empty;
                return;
            }

            if (!_keyboardScenarioIds.Contains(_keyboardScenarioId))
            {
                _keyboardScenarioId =
                    _keyboardScenarioIds.Contains(_selectedId)
                        ? _selectedId
                        : _keyboardScenarioIds[0];
            }
            RefreshKeyboardHighlight();
        }

        private void BuildRecentSection()
        {
            var indexes = new List<int>(MaxRecentItems);
            for (int recentIndex = 0;
                 recentIndex < _recentScenarioIds.Count;
                 recentIndex++)
            {
                int itemIndex = FindItemIndex(
                    _recentScenarioIds[recentIndex]);
                if (itemIndex >= 0)
                    indexes.Add(itemIndex);
            }

            if (indexes.Count > 0)
                BuildSection("最近使用", "recent", indexes);
        }

        private void BuildGroupSection(string groupName, string query)
        {
            var indexes = new List<int>();
            for (int i = 0; i < _items.Count; i++)
            {
                TriggerTestScenarioPickerItem item = _items[i];
                string actualGroup = string.IsNullOrEmpty(item.GroupName)
                    ? "其他"
                    : item.GroupName;
                if (!string.Equals(
                        actualGroup,
                        groupName,
                        StringComparison.Ordinal) ||
                    !MatchesFilter(item) ||
                    !MatchesQuery(item, query))
                {
                    continue;
                }

                indexes.Add(i);
            }

            if (indexes.Count > 0)
            {
                BuildSection(
                    groupName,
                    GetGroupClassSuffix(groupName),
                    indexes);
            }
        }

        private void BuildSection(
            string title,
            string suffix,
            IReadOnlyList<int> itemIndexes)
        {
            var section = new VisualElement
            {
                name = "trigger-test-scenario-group-" + suffix
            };
            section.AddToClassList(
                "trigger-test-scenario-group");
            var heading = new Label(title);
            heading.AddToClassList(
                "trigger-test-scenario-group-title");
            section.Add(heading);
            for (int i = 0; i < itemIndexes.Count; i++)
            {
                TriggerTestScenarioPickerItem item =
                    _items[itemIndexes[i]];
                section.Add(BuildItemRow(item));
                if (!_keyboardScenarioIds.Contains(item.ScenarioId))
                    _keyboardScenarioIds.Add(item.ScenarioId);
            }
            _list.Add(section);
        }

        private Button BuildItemRow(TriggerTestScenarioPickerItem item)
        {
            string scenarioId = item.ScenarioId;
            var row = new Button(() => Select(scenarioId))
            {
                name =
                    "trigger-test-scenario-option-" +
                    GetSafeName(scenarioId),
                userData = scenarioId,
                tooltip = item.CanRun
                    ? item.ScenarioId
                    : item.UnavailableReason + "\n" + item.ScenarioId
            };
            row.AddToClassList("trigger-test-scenario-option");
            row.EnableInClassList(
                "is-selected",
                string.Equals(
                    scenarioId,
                    _selectedId,
                    StringComparison.Ordinal));
            row.EnableInClassList("is-unavailable", !item.CanRun);

            var check = new Label(
                string.Equals(
                    scenarioId,
                    _selectedId,
                    StringComparison.Ordinal)
                    ? "✓"
                    : string.Empty);
            check.AddToClassList(
                "trigger-test-scenario-option-check");
            row.Add(check);
            var nameLabel = new Label(item.DisplayName);
            nameLabel.AddToClassList(
                "trigger-test-scenario-option-name");
            row.Add(nameLabel);
            Label profile = BuildBadge(GetProfileLabel(item.Profile));
            profile.AddToClassList("is-profile");
            row.Add(profile);
            Label availability = BuildBadge(
                GetAvailabilityLabel(item));
            availability.AddToClassList(
                item.CanRun ? "is-available" : "is-blocked");
            row.Add(availability);
            return row;
        }

        private static Label BuildBadge(string text)
        {
            var badge = new Label(text);
            badge.AddToClassList("trigger-test-scenario-badge");
            return badge;
        }

        private void Select(string scenarioId)
        {
            if (!ContainsScenario(scenarioId))
                return;

            bool changed = !string.Equals(
                _selectedId,
                scenarioId,
                StringComparison.Ordinal);
            _selectedId = scenarioId;
            _keyboardScenarioId = scenarioId;
            Remember(scenarioId);
            RefreshSelectionButton();
            Close();
            if (changed)
                SelectionChanged?.Invoke(scenarioId);
        }

        private void Remember(string scenarioId)
        {
            _recentScenarioIds.Remove(scenarioId);
            _recentScenarioIds.Insert(0, scenarioId);
            if (_recentScenarioIds.Count > MaxRecentItems)
            {
                _recentScenarioIds.RemoveRange(
                    MaxRecentItems,
                    _recentScenarioIds.Count - MaxRecentItems);
            }
        }

        private void RefreshSelectionButton()
        {
            int index = FindItemIndex(_selectedId);
            if (index < 0)
            {
                _selectedName.text = "没有可用 Trigger";
                _selectedProfile.text = string.Empty;
                _selectedProfile.style.display = DisplayStyle.None;
                _selectionButton.tooltip = "没有可选择的 Trigger";
                return;
            }

            TriggerTestScenarioPickerItem item = _items[index];
            _selectedName.text = item.DisplayName;
            _selectedProfile.text = GetProfileLabel(item.Profile);
            _selectedProfile.style.display = DisplayStyle.Flex;
            _selectionButton.tooltip = item.CanRun
                ? "当前 Trigger：" + item.DisplayName
                : item.UnavailableReason;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!_isOpen)
            {
                if (evt.keyCode == KeyCode.Return ||
                    evt.keyCode == KeyCode.KeypadEnter ||
                    evt.keyCode == KeyCode.Space)
                {
                    Open();
                    evt.StopPropagation();
                }
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    Close();
                    _selectionButton.Focus();
                    evt.StopPropagation();
                    break;
                case KeyCode.UpArrow:
                    MoveKeyboardSelection(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    MoveKeyboardSelection(1);
                    evt.StopPropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (!string.IsNullOrEmpty(_keyboardScenarioId))
                        Select(_keyboardScenarioId);
                    evt.StopPropagation();
                    break;
            }
        }

        private void MoveKeyboardSelection(int direction)
        {
            if (_keyboardScenarioIds.Count == 0)
                return;

            int index = _keyboardScenarioIds.IndexOf(
                _keyboardScenarioId);
            if (index < 0)
                index = 0;
            else
            {
                index =
                    (index + direction + _keyboardScenarioIds.Count) %
                    _keyboardScenarioIds.Count;
            }
            _keyboardScenarioId = _keyboardScenarioIds[index];
            RefreshKeyboardHighlight();
        }

        private void RefreshKeyboardHighlight()
        {
            UQueryBuilder<Button> rows =
                _list.Query<Button>(
                    className: "trigger-test-scenario-option");
            rows.ForEach(row =>
            {
                string scenarioId = GetScenarioId(row);
                row.EnableInClassList(
                    "is-keyboard-selected",
                    string.Equals(
                        scenarioId,
                        _keyboardScenarioId,
                        StringComparison.Ordinal));
            });
        }

        private bool MatchesFilter(TriggerTestScenarioPickerItem item)
        {
            switch (_filter)
            {
                case TriggerTestScenarioPickerFilter.Available:
                    return item.CanRun;
                case TriggerTestScenarioPickerFilter.EditMode:
                    return item.Profile ==
                        TriggerTestExecutionProfile.EditMode;
                case TriggerTestScenarioPickerFilter.PlayMode:
                    return item.Profile ==
                        TriggerTestExecutionProfile.PlayMode;
                default:
                    return true;
            }
        }

        private static bool MatchesQuery(
            TriggerTestScenarioPickerItem item,
            string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            return item.DisplayName.IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.GroupName.IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.ScenarioId.IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool ContainsScenario(string scenarioId) =>
            FindItemIndex(scenarioId) >= 0;

        private int FindItemIndex(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return -1;

            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(
                        _items[i].ScenarioId,
                        scenarioId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveMissingRecentItems()
        {
            for (int i = _recentScenarioIds.Count - 1; i >= 0; i--)
            {
                if (!ContainsScenario(_recentScenarioIds[i]))
                    _recentScenarioIds.RemoveAt(i);
            }
        }

        private static string GetProfileLabel(
            TriggerTestExecutionProfile profile)
        {
            switch (profile)
            {
                case TriggerTestExecutionProfile.EditMode:
                    return "Edit";
                case TriggerTestExecutionProfile.PlayMode:
                    return "Play";
                default:
                    return "NoEngine";
            }
        }

        private static string GetAvailabilityLabel(
            TriggerTestScenarioPickerItem item)
        {
            if (item.CanRun)
                return "可执行";
            if (item.UnavailableReason.IndexOf(
                    "Play Mode",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "需进入 Play Mode";
            }
            if (item.UnavailableReason.IndexOf(
                    "Edit Mode",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "需回到 Edit Mode";
            }

            return "不可执行";
        }

        private static string GetGroupClassSuffix(string groupName)
        {
            switch (groupName)
            {
                case "环境准备":
                    return "environment";
                case "测试控制":
                    return "control";
                case "时间轴观察":
                    return "timeline";
                case "实验设置":
                    return "setup";
                default:
                    return "other";
            }
        }

        private static string GetSafeName(string scenarioId)
        {
            int separator = scenarioId.LastIndexOf('.');
            string slug = separator >= 0
                ? scenarioId.Substring(separator + 1)
                : scenarioId;
            return slug.Replace('_', '-');
        }

        private static string GetScenarioId(VisualElement row)
            => row?.userData as string ?? string.Empty;

        private enum TriggerTestScenarioPickerFilter
        {
            All = 0,
            Available = 1,
            EditMode = 2,
            PlayMode = 3
        }
    }
}
