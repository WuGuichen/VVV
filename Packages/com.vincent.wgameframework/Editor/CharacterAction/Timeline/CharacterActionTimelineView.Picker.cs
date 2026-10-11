using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed partial class CharacterActionTimelineView
    {
        private void BuildItemPickerOverlay()
        {
            _itemPickerBackdrop = new VisualElement
            {
                name = "timeline-current-item-picker-backdrop",
            };
            _itemPickerBackdrop.AddToClassList("ca-tl-picker-backdrop");
            _itemPickerBackdrop.style.display = DisplayStyle.None;
            _itemPickerBackdrop.pickingMode = PickingMode.Position;
            _itemPickerBackdrop.RegisterCallback<PointerDownEvent>(OnItemPickerBackdropPointerDown);
            Add(_itemPickerBackdrop);

            _itemPickerOverlay = new VisualElement
            {
                name = "timeline-current-item-picker-overlay",
            };
            _itemPickerOverlay.AddToClassList("ca-tl-picker-overlay");
            _itemPickerOverlay.style.display = DisplayStyle.None;
            _itemPickerOverlay.focusable = true;
            _itemPickerOverlay.RegisterCallback<KeyDownEvent>(OnItemPickerKeyDown);
            _itemPickerOverlay.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            var header = new VisualElement
            {
                name = "timeline-current-item-picker-header",
            };
            header.AddToClassList("ca-tl-picker-header");

            var headerTitle = new Label("\u641c\u7d22\u65f6\u95f4\u8f74");
            headerTitle.AddToClassList("ca-tl-picker-header-title");
            header.Add(headerTitle);

            var closeButton = new Button(CloseItemPicker) { text = "\u00d7" };
            closeButton.name = "timeline-current-item-picker-close";
            closeButton.AddToClassList("ca-tl-picker-close-btn");
            closeButton.tooltip = "\u5173\u95ed (Esc)";
            header.Add(closeButton);
            _itemPickerOverlay.Add(header);

            _itemPickerSearch = new ToolbarSearchField
            {
                name = "timeline-current-item-picker-search",
            };
            _itemPickerSearch.AddToClassList("ca-tl-picker-search");
            _itemPickerSearch.RegisterValueChangedCallback(evt =>
            {
                _pickerSearchText = evt.newValue ?? string.Empty;
                RebuildItemPickerList();
            });
            _itemPickerSearch.RegisterCallback<KeyDownEvent>(OnItemPickerKeyDown, TrickleDown.TrickleDown);
            _itemPickerOverlay.Add(_itemPickerSearch);

            _itemPickerScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "timeline-current-item-picker-scroll",
            };
            _itemPickerScroll.AddToClassList("ca-tl-picker-scroll");
            _itemPickerList = new VisualElement
            {
                name = "timeline-current-item-picker-list",
            };
            _itemPickerList.AddToClassList("ca-tl-picker-list");
            _itemPickerScroll.Add(_itemPickerList);
            _itemPickerOverlay.Add(_itemPickerScroll);

            _itemPickerFooter = new VisualElement
            {
                name = "timeline-current-item-picker-footer",
            };
            _itemPickerFooter.AddToClassList("ca-tl-picker-footer");
            _itemPickerOverlay.Add(_itemPickerFooter);

            Add(_itemPickerOverlay);
        }

        private void RefreshContextSelector()
        {
            if (_contextSelector == null)
                return;

            bool hasProvider = _host?.PickerItemsProvider != null && _host.PickerItemSelected != null;
            string label = _host?.CurrentItemLabelProvider?.Invoke();
            if (string.IsNullOrWhiteSpace(label))
                label = "（未选择）";
            _contextSelector.text = label;
            _contextSelector.tooltip = hasProvider
                ? "选择要编辑的动作或效果时间轴（Ctrl+K 搜索）"
                : "当前模式不支持切换时间轴。";
            _contextSelector.SetEnabled(hasProvider);
            RebuildContextSelectorMenu();
        }

        private void RebuildContextSelectorMenu()
        {
            if (_contextSelector == null)
                return;

            _contextSelector.menu.MenuItems().Clear();
            bool hasProvider = _host?.PickerItemsProvider != null && _host.PickerItemSelected != null;
            if (!hasProvider)
                return;

            _contextSelector.menu.AppendAction(
                "搜索…",
                _ => OpenItemPicker(),
                DropdownMenuAction.AlwaysEnabled);

            string currentId = CurrentPickerItemId();
            IReadOnlyList<CharacterActionTimelinePickerItem> items =
                _host.PickerItemsProvider?.Invoke() ?? Array.Empty<CharacterActionTimelinePickerItem>();
            CharacterActionTimelinePickerItem currentItem = FindPickerItem(items, currentId);
            if (currentItem != null && !string.IsNullOrWhiteSpace(currentItem.Subtitle))
            {
                _contextSelector.menu.AppendSeparator();
                _contextSelector.menu.AppendAction(
                    currentItem.Subtitle,
                    _ => { },
                    DropdownMenuAction.Status.Disabled);
            }

            _contextSelector.menu.AppendSeparator();
            var shown = new HashSet<string>(StringComparer.Ordinal);
            AppendContextSelectorSection("收藏", FilterPickerItems(items, item => item.IsFavorite, shown));
            AppendContextSelectorSection("最近", FilterPickerItems(items, item => item.IsRecent, shown));
            AppendContextSelectorSection("全部", FilterPickerItems(items, _ => true, shown));

            if (_host?.NavigateToItemProvider != null && !string.IsNullOrWhiteSpace(currentId))
            {
                _contextSelector.menu.AppendSeparator();
                _contextSelector.menu.AppendAction(
                    "在动作列表中定位",
                    _ => _host.NavigateToItemProvider.Invoke(currentId),
                    DropdownMenuAction.AlwaysEnabled);
            }
        }

        private void AppendContextSelectorSection(string title, List<CharacterActionTimelinePickerItem> sectionItems)
        {
            if (sectionItems == null || sectionItems.Count == 0)
                return;

            _contextSelector.menu.AppendSeparator();
            _contextSelector.menu.AppendAction(title, _ => { }, DropdownMenuAction.Status.Disabled);
            string currentId = CurrentPickerItemId();
            for (int i = 0; i < sectionItems.Count; i++)
            {
                CharacterActionTimelinePickerItem item = sectionItems[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                    continue;

                string menuLabel = BuildContextSelectorMenuLabel(item);
                string itemId = item.Id;
                _contextSelector.menu.AppendAction(
                    menuLabel,
                    _ => SelectPickerItemById(itemId),
                    _ => string.Equals(itemId, currentId, StringComparison.Ordinal)
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
        }

        private static string BuildContextSelectorMenuLabel(CharacterActionTimelinePickerItem item)
        {
            string prefix = item.IsFavorite ? "★ " : item.IsRecent ? "◷ " : string.Empty;
            string title = string.IsNullOrWhiteSpace(item.Title) ? item.Id : item.Title;
            if (item.IsDirty)
                title += " *";
            return prefix + title;
        }

        private static CharacterActionTimelinePickerItem FindPickerItem(
            IReadOnlyList<CharacterActionTimelinePickerItem> items,
            string itemId)
        {
            if (items == null || string.IsNullOrWhiteSpace(itemId))
                return null;

            for (int i = 0; i < items.Count; i++)
            {
                CharacterActionTimelinePickerItem item = items[i];
                if (item != null && string.Equals(item.Id, itemId, StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        private void SelectPickerItemById(string itemId)
        {
            IReadOnlyList<CharacterActionTimelinePickerItem> items =
                _host?.PickerItemsProvider?.Invoke() ?? Array.Empty<CharacterActionTimelinePickerItem>();
            CharacterActionTimelinePickerItem item = FindPickerItem(items, itemId);
            if (item != null)
                SelectPickerItem(item);
        }

        private void RefreshItemPickerButton()
        {
            RefreshContextSelector();
        }

        private void ToggleItemPicker()
        {
            if (_itemPickerOverlay == null)
                return;

            bool isOpen = _itemPickerOverlay.style.display == DisplayStyle.Flex;
            if (isOpen)
                CloseItemPicker();
            else
                OpenItemPicker();
        }

        private bool IsItemPickerOpen()
        {
            return _itemPickerOverlay != null
                && _itemPickerOverlay.style.display == DisplayStyle.Flex;
        }

        private void OnItemPickerBackdropPointerDown(PointerDownEvent evt)
        {
            if (!IsItemPickerOpen())
                return;

            CloseItemPicker();
            evt.StopPropagation();
        }

        private void OpenItemPicker(string searchText = "")
        {
            if (_itemPickerOverlay == null
                || _host?.PickerItemsProvider == null
                || _host.PickerItemSelected == null)
            {
                return;
            }

            _pickerSearchText = searchText ?? string.Empty;
            _itemPickerSearch?.SetValueWithoutNotify(_pickerSearchText);
            if (_itemPickerBackdrop != null)
                _itemPickerBackdrop.style.display = DisplayStyle.Flex;
            _itemPickerOverlay.style.display = DisplayStyle.Flex;
            PositionItemPickerOverlay();
            RebuildItemPickerList();
            _itemPickerBackdrop?.BringToFront();
            _itemPickerOverlay.BringToFront();
            _itemPickerOverlay.schedule.Execute(() => _itemPickerSearch?.Focus());
        }

        private void PositionItemPickerOverlay()
        {
            if (_itemPickerOverlay == null || _contextSelector == null)
                return;

            float rootWidth = resolvedStyle.width > 1f ? resolvedStyle.width : worldBound.width;
            if (rootWidth <= 1f)
                rootWidth = 640f;

            float overlayWidth = Mathf.Min(440f, Mathf.Max(280f, rootWidth - 24f));
            _itemPickerOverlay.style.width = overlayWidth;

            Rect anchorBounds = _contextSelector.worldBound;
            Vector2 local = this.WorldToLocal(new Vector2(anchorBounds.xMin, anchorBounds.yMax + 4f));
            float maxLeft = Mathf.Max(8f, rootWidth - overlayWidth - 8f);
            _itemPickerOverlay.style.left = Mathf.Clamp(local.x, 8f, maxLeft);
            _itemPickerOverlay.style.top = Mathf.Max(28f, local.y);
        }

        private void CloseItemPicker()
        {
            if (_itemPickerOverlay == null)
                return;

            if (_itemPickerBackdrop != null)
                _itemPickerBackdrop.style.display = DisplayStyle.None;
            _itemPickerOverlay.style.display = DisplayStyle.None;
            _pickerFocusedIndex = -1;
            _pickerVisibleItems.Clear();
            _itemPickerList?.Clear();
            _itemPickerFooter?.Clear();
            Focus();
        }

        private void RebuildItemPickerList()
        {
            if (_itemPickerList == null)
                return;

            _itemPickerList.Clear();
            _itemPickerFooter?.Clear();
            _pickerVisibleItems.Clear();
            _pickerFocusedIndex = -1;

            IReadOnlyList<CharacterActionTimelinePickerItem> items =
                _host?.PickerItemsProvider?.Invoke() ?? Array.Empty<CharacterActionTimelinePickerItem>();
            string query = _pickerSearchText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
            {
                var matches = new List<CharacterActionTimelinePickerItem>();
                for (int i = 0; i < items.Count; i++)
                {
                    CharacterActionTimelinePickerItem item = items[i];
                    if (PickerItemMatches(item, query))
                        matches.Add(item);
                }

                matches.Sort((left, right) =>
                {
                    int score = PickerMatchScore(right, query).CompareTo(PickerMatchScore(left, query));
                    return score != 0
                        ? score
                        : string.Compare(left?.Title, right?.Title, StringComparison.OrdinalIgnoreCase);
                });
                AddPickerSection("搜索结果", matches);
            }
            else
            {
                var shown = new HashSet<string>(StringComparer.Ordinal);
                AddPickerSection("收藏", FilterPickerItems(items, item => item.IsFavorite, shown));
                AddPickerSection("最近", FilterPickerItems(items, item => item.IsRecent, shown));
                AddPickerSection("全部", FilterPickerItems(items, _ => true, shown));
            }

            if (_pickerVisibleItems.Count == 0)
            {
                var empty = new Label("没有匹配的条目。");
                empty.AddToClassList("ca-tl-picker-empty");
                _itemPickerList.Add(empty);
            }
            else
            {
                SetPickerFocus(0);
            }

            _host?.PickerFooterBuilder?.Invoke(_itemPickerFooter);
        }

        private static List<CharacterActionTimelinePickerItem> FilterPickerItems(
            IReadOnlyList<CharacterActionTimelinePickerItem> items,
            Func<CharacterActionTimelinePickerItem, bool> predicate,
            HashSet<string> shown)
        {
            var result = new List<CharacterActionTimelinePickerItem>();
            if (items == null)
                return result;

            for (int i = 0; i < items.Count; i++)
            {
                CharacterActionTimelinePickerItem item = items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Id) || shown.Contains(item.Id))
                    continue;
                if (!predicate(item))
                    continue;

                shown.Add(item.Id);
                result.Add(item);
            }

            result.Sort((left, right) => string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private void AddPickerSection(string title, List<CharacterActionTimelinePickerItem> items)
        {
            if (items == null || items.Count == 0)
                return;

            var header = new Label(title);
            header.AddToClassList("ca-tl-picker-section");
            _itemPickerList.Add(header);
            for (int i = 0; i < items.Count; i++)
                AddPickerRow(items[i]);
        }

        private void AddPickerRow(CharacterActionTimelinePickerItem item)
        {
            if (item == null)
                return;

            int index = _pickerVisibleItems.Count;
            _pickerVisibleItems.Add(item);
            var row = new VisualElement
            {
                name = "timeline-current-item-picker-row-" + item.Id,
                userData = item,
            };
            row.AddToClassList("ca-tl-picker-row");
            bool isCurrent = string.Equals(CurrentPickerItemId(), item.Id, StringComparison.Ordinal);
            row.EnableInClassList("ca-tl-picker-row--current", isCurrent);
            row.RegisterCallback<MouseEnterEvent>(_ => SetPickerFocus(index));
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                evt.StopPropagation();
                SelectPickerItem(item);
            });

            var star = new Button(() => TogglePickerFavorite(item))
            {
                name = "timeline-current-item-picker-favorite-" + item.Id,
                text = item.IsFavorite ? "*" : "+",
                tooltip = item.IsFavorite ? "取消收藏" : "添加收藏",
            };
            star.AddToClassList("ca-tl-picker-star");
            row.Add(star);

            var textColumn = new VisualElement();
            textColumn.AddToClassList("ca-tl-picker-text");
            var title = new Label(string.IsNullOrWhiteSpace(item.Title) ? item.Id : item.Title);
            title.AddToClassList("ca-tl-picker-title");
            textColumn.Add(title);
            if (!string.IsNullOrWhiteSpace(item.Subtitle))
            {
                var subtitle = new Label(item.Subtitle);
                subtitle.AddToClassList("ca-tl-picker-subtitle");
                textColumn.Add(subtitle);
            }

            row.Add(textColumn);

            if (item.IsDirty)
            {
                var dirty = new Label("未保存");
                dirty.AddToClassList("ca-tl-picker-badge");
                row.Add(dirty);
            }

            if (isCurrent)
            {
                var current = new Label("当前");
                current.AddToClassList("ca-tl-picker-badge");
                row.Add(current);
            }

            _itemPickerList.Add(row);
        }

        private static bool PickerItemMatches(CharacterActionTimelinePickerItem item, string query)
        {
            if (item == null)
                return false;
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string haystack = (item.Id + " " + item.Title + " " + item.Subtitle + " " + item.SearchText)
                .ToLowerInvariant();
            string[] tokens = query.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (haystack.IndexOf(tokens[i], StringComparison.Ordinal) < 0)
                    return false;
            }

            return true;
        }

        private static int PickerMatchScore(CharacterActionTimelinePickerItem item, string query)
        {
            if (item == null || string.IsNullOrWhiteSpace(query))
                return 0;

            string normalized = query.Trim().ToLowerInvariant();
            string id = (item.Id ?? string.Empty).ToLowerInvariant();
            string title = (item.Title ?? string.Empty).ToLowerInvariant();
            if (string.Equals(id, normalized, StringComparison.Ordinal))
                return 100;
            if (string.Equals(title, normalized, StringComparison.Ordinal))
                return 90;
            if (id.StartsWith(normalized, StringComparison.Ordinal))
                return 80;
            if (title.StartsWith(normalized, StringComparison.Ordinal))
                return 70;
            if (id.IndexOf(normalized, StringComparison.Ordinal) >= 0)
                return 60;
            if (title.IndexOf(normalized, StringComparison.Ordinal) >= 0)
                return 50;
            return 10;
        }

        private string CurrentPickerItemId()
        {
            return _host?.CurrentItemIdProvider?.Invoke() ?? string.Empty;
        }

        private void SelectPickerItem(CharacterActionTimelinePickerItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                return;

            _host?.PickerRecentRecorded?.Invoke(item.Id);
            _host?.PickerItemSelected?.Invoke(item.Id);
            CloseItemPicker();
        }

        private void TogglePickerFavorite(CharacterActionTimelinePickerItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                return;

            _host?.PickerFavoriteChanged?.Invoke(item.Id, !item.IsFavorite);
            RebuildItemPickerList();
        }

        private void OnItemPickerKeyDown(KeyDownEvent evt)
        {
            if (_itemPickerOverlay == null || _itemPickerOverlay.style.display == DisplayStyle.None)
                return;

            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    CloseItemPicker();
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    MovePickerFocus(1);
                    evt.StopPropagation();
                    break;
                case KeyCode.UpArrow:
                    MovePickerFocus(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_pickerFocusedIndex >= 0 && _pickerFocusedIndex < _pickerVisibleItems.Count)
                    {
                        SelectPickerItem(_pickerVisibleItems[_pickerFocusedIndex]);
                        evt.StopPropagation();
                    }
                    break;
            }
        }

        private void MovePickerFocus(int delta)
        {
            if (_pickerVisibleItems.Count == 0)
                return;

            int next = _pickerFocusedIndex < 0
                ? 0
                : (_pickerFocusedIndex + delta + _pickerVisibleItems.Count) % _pickerVisibleItems.Count;
            SetPickerFocus(next);
        }

        private void SetPickerFocus(int index)
        {
            _pickerFocusedIndex = _pickerVisibleItems.Count == 0
                ? -1
                : Mathf.Clamp(index, 0, _pickerVisibleItems.Count - 1);
            RefreshPickerFocusVisuals();
        }

        private void RefreshPickerFocusVisuals()
        {
            if (_itemPickerList == null)
                return;

            string focusedId = _pickerFocusedIndex >= 0 && _pickerFocusedIndex < _pickerVisibleItems.Count
                ? _pickerVisibleItems[_pickerFocusedIndex].Id
                : string.Empty;
            _itemPickerList.Query<VisualElement>(className: "ca-tl-picker-row").ForEach(row =>
            {
                var item = row.userData as CharacterActionTimelinePickerItem;
                row.EnableInClassList(
                    "ca-tl-picker-row--focused",
                    item != null && string.Equals(item.Id, focusedId, StringComparison.Ordinal));
            });
        }

        internal void OpenItemPickerForTests(string searchText = "")
        {
            OpenItemPicker(searchText);
        }

        internal void CloseItemPickerForTests()
        {
            CloseItemPicker();
        }

        internal bool IsItemPickerOpenForTests()
        {
            return IsItemPickerOpen();
        }

        internal string[] VisiblePickerItemIdsForTests()
        {
            var ids = new string[_pickerVisibleItems.Count];
            for (int i = 0; i < _pickerVisibleItems.Count; i++)
                ids[i] = _pickerVisibleItems[i].Id;
            return ids;
        }

        internal void SelectPickerItemForTests(string itemId)
        {
            IReadOnlyList<CharacterActionTimelinePickerItem> items =
                _host?.PickerItemsProvider?.Invoke() ?? Array.Empty<CharacterActionTimelinePickerItem>();
            for (int i = 0; i < items.Count; i++)
            {
                CharacterActionTimelinePickerItem item = items[i];
                if (item != null && string.Equals(item.Id, itemId, StringComparison.Ordinal))
                {
                    SelectPickerItem(item);
                    return;
                }
            }
        }

        internal void TogglePickerFavoriteForTests(string itemId)
        {
            IReadOnlyList<CharacterActionTimelinePickerItem> items =
                _host?.PickerItemsProvider?.Invoke() ?? Array.Empty<CharacterActionTimelinePickerItem>();
            for (int i = 0; i < items.Count; i++)
            {
                CharacterActionTimelinePickerItem item = items[i];
                if (item != null && string.Equals(item.Id, itemId, StringComparison.Ordinal))
                {
                    TogglePickerFavorite(item);
                    return;
                }
            }
        }
    }
}
