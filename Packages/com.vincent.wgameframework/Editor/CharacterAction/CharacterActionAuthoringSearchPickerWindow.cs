using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed class CharacterActionAuthoringSearchPickerWindow : EditorWindow
    {
        private const float WindowWidth = 540f;
        private const float WindowHeight = 430f;
        private const float RowHeight = 52f;

        private CharacterActionAuthoringSearchCatalog _catalog;
        private CharacterActionAuthoringSearchCategory _category;
        private string _currentValue = string.Empty;
        private string _pickerTitle = string.Empty;
        private bool _allowClear;
        private Action<string> _onSelected;
        private CharacterActionAuthoringSearchProjection _projection;
        private TextField _searchField;
        private ListView _listView;
        private Label _summaryLabel;
        private Label _emptyLabel;
        private Button _selectButton;

        internal static void ShowPicker(
            Rect activatorScreenRect,
            string title,
            CharacterActionAuthoringSearchCatalog catalog,
            CharacterActionAuthoringSearchCategory category,
            string currentValue,
            bool allowClear,
            Action<string> onSelected)
        {
            var window = CreateInstance<CharacterActionAuthoringSearchPickerWindow>();
            window.Initialize(title, catalog, category, currentValue, allowClear, onSelected);
            window.ShowAsDropDown(activatorScreenRect, new Vector2(WindowWidth, WindowHeight));
        }

        public void CreateGUI()
        {
            BuildUi();
        }

        private void Initialize(
            string title,
            CharacterActionAuthoringSearchCatalog catalog,
            CharacterActionAuthoringSearchCategory category,
            string currentValue,
            bool allowClear,
            Action<string> onSelected)
        {
            _pickerTitle = string.IsNullOrWhiteSpace(title) ? "选择引用" : title;
            _catalog = catalog ?? new CharacterActionAuthoringSearchCatalog(null);
            _category = category;
            _currentValue = currentValue ?? string.Empty;
            _allowClear = allowClear;
            _onSelected = onSelected;
            titleContent = new GUIContent(_pickerTitle);
            if (rootVisualElement.childCount > 0)
                BuildUi();
        }

        private void BuildUi()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            var title = new Label(_pickerTitle);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14f;
            title.style.marginBottom = 6f;
            root.Add(title);

            _searchField = new TextField
            {
                name = "character-action-search-picker-query",
                isDelayed = false,
            };
            _searchField.label = "搜索";
            _searchField.tooltip = "名称、stable id、来源和说明支持空格分隔的多词搜索";
            _searchField.RegisterValueChangedCallback(_ => RefreshProjection());
            root.Add(_searchField);

            _summaryLabel = new Label();
            _summaryLabel.style.marginTop = 4f;
            _summaryLabel.style.marginBottom = 4f;
            _summaryLabel.style.color = new Color(0.65f, 0.68f, 0.72f);
            root.Add(_summaryLabel);

            _listView = new ListView
            {
                name = "character-action-search-picker-list",
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = RowHeight,
                makeItem = MakeCandidateRow,
                bindItem = BindCandidateRow,
            };
            _listView.style.flexGrow = 1f;
            _listView.style.minHeight = 220f;
            _listView.selectionChanged += _ => RefreshSelectButton();
            _listView.itemsChosen += ChooseItems;
            root.Add(_listView);

            _emptyLabel = new Label("没有匹配项。数据源不可用时，当前旧值仍会作为“未知引用”保留。")
            {
                name = "character-action-search-picker-empty",
            };
            _emptyLabel.style.whiteSpace = WhiteSpace.Normal;
            _emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _emptyLabel.style.marginTop = 20f;
            _emptyLabel.style.marginBottom = 20f;
            root.Add(_emptyLabel);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.marginTop = 8f;
            if (_allowClear)
            {
                var clearButton = new Button(() => Commit(string.Empty)) { text = "清除" };
                clearButton.name = "character-action-search-picker-clear";
                clearButton.style.marginRight = 6f;
                footer.Add(clearButton);
            }

            footer.Add(new Button(Close) { text = "取消" });
            _selectButton = new Button(ChooseSelected) { text = "选择" };
            _selectButton.name = "character-action-search-picker-select";
            _selectButton.style.marginLeft = 6f;
            footer.Add(_selectButton);
            root.Add(footer);

            RefreshProjection();
            _searchField.schedule.Execute(() => _searchField.Focus()).ExecuteLater(1);
        }

        private VisualElement MakeCandidateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;

            var text = new VisualElement();
            text.style.flexGrow = 1f;
            text.style.minWidth = 0f;
            var displayName = new Label { name = "display-name" };
            displayName.style.unityFontStyleAndWeight = FontStyle.Bold;
            displayName.style.overflow = Overflow.Hidden;
            text.Add(displayName);
            var detail = new Label { name = "detail" };
            detail.style.color = new Color(0.63f, 0.67f, 0.72f);
            detail.style.overflow = Overflow.Hidden;
            text.Add(detail);
            row.Add(text);

            var source = new Label { name = "source" };
            source.style.minWidth = 90f;
            source.style.maxWidth = 150f;
            source.style.unityTextAlign = TextAnchor.MiddleRight;
            source.style.color = new Color(0.48f, 0.62f, 0.78f);
            source.style.overflow = Overflow.Hidden;
            row.Add(source);
            return row;
        }

        private void BindCandidateRow(VisualElement row, int index)
        {
            if (_projection == null || index < 0 || index >= _projection.Candidates.Length)
                return;

            CharacterActionAuthoringSearchCandidate candidate = _projection.Candidates[index];
            row.Q<Label>("display-name").text = candidate.DisplayName;
            string detail = candidate.Value;
            if (!string.IsNullOrWhiteSpace(candidate.SecondaryText))
                detail += "  ·  " + candidate.SecondaryText;
            row.Q<Label>("detail").text = detail;
            row.Q<Label>("source").text = candidate.IsKnown
                ? candidate.SourceName
                : "未知引用";
            row.EnableInClassList("ca-search-candidate--unknown", !candidate.IsKnown);
            row.tooltip = string.IsNullOrWhiteSpace(candidate.SourceName)
                ? candidate.Value
                : candidate.Value + "\n" + candidate.SourceName;
        }

        private void RefreshProjection()
        {
            _projection = (_catalog ?? new CharacterActionAuthoringSearchCatalog(null)).Project(
                _category,
                _searchField?.value ?? string.Empty,
                _currentValue);
            _listView.itemsSource = _projection.Candidates;
            _listView.Rebuild();
            bool hasItems = _projection.Candidates.Length > 0;
            _listView.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
            _summaryLabel.text = _projection.HasUnknownCurrentValue
                ? _projection.Candidates.Length + " 项；当前值未被数据源识别，但会保留"
                : _projection.Candidates.Length + " 项";
            _listView.selectedIndex = hasItems ? 0 : -1;
            RefreshSelectButton();
        }

        private void RefreshSelectButton()
        {
            _selectButton?.SetEnabled(
                _projection != null
                && _listView != null
                && _listView.selectedIndex >= 0
                && _listView.selectedIndex < _projection.Candidates.Length);
        }

        private void ChooseItems(IEnumerable<object> items)
        {
            if (items == null)
                return;
            foreach (object item in items)
            {
                if (item is CharacterActionAuthoringSearchCandidate candidate)
                {
                    Commit(candidate.Value);
                    return;
                }
            }
        }

        private void ChooseSelected()
        {
            int index = _listView?.selectedIndex ?? -1;
            if (_projection == null || index < 0 || index >= _projection.Candidates.Length)
                return;
            Commit(_projection.Candidates[index].Value);
        }

        private void Commit(string value)
        {
            Action<string> callback = _onSelected;
            _onSelected = null;
            callback?.Invoke(value ?? string.Empty);
            Close();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                ChooseSelected();
                evt.StopPropagation();
            }
        }

        private void OnDisable()
        {
            _onSelected = null;
        }
    }
}
