using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void AddActionSetSearchPickerField(
            VisualElement parent,
            string label,
            string value,
            CharacterActionAuthoringSearchCatalog catalog,
            CharacterActionAuthoringSearchCategory category,
            Action<string> setter,
            bool allowClear = false,
            UiRefreshScope refreshScope = UiRefreshScope.Content)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var caption = new Label(label);
            caption.style.minWidth = 105f;
            row.Add(caption);

            var currentValue = new Label(string.IsNullOrWhiteSpace(value) ? "（未设置）" : value);
            currentValue.tooltip = value ?? string.Empty;
            currentValue.style.flexGrow = 1f;
            currentValue.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(currentValue);

            var field = new TextField("手动 ID")
            {
                value = value ?? string.Empty,
                isDelayed = true,
            };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt =>
                ApplyActionSetDocumentChange(
                    () => setter(evt.newValue ?? string.Empty),
                    refreshScope));

            CharacterActionAuthoringSearchProjection current = catalog.Project(category, string.Empty, value);
            if (current.HasUnknownCurrentValue)
            {
                var unknown = new Label("未知引用（保留）");
                unknown.style.color = new Color(0.9f, 0.67f, 0.25f);
                unknown.style.marginLeft = 6f;
                unknown.style.marginBottom = 4f;
                row.Add(unknown);
            }

            var picker = new Button { text = "选择…" };
            picker.style.marginLeft = 6f;
            picker.clicked += () => OpenSearchPicker(
                picker,
                "选择" + label,
                catalog,
                category,
                field.value,
                allowClear,
                selected => ApplyActionSetDocumentChange(
                    () => setter(selected),
                    refreshScope));
            row.Add(picker);
            parent.Add(row);

            var advanced = new Foldout
            {
                text = "高级：手动输入 " + label,
                value = false,
            };
            advanced.Add(field);
            parent.Add(advanced);
        }

        private void AddActionSetTagPickerField(
            VisualElement parent,
            string label,
            string[] values,
            Action<string[]> setter)
        {
            values = values ?? Array.Empty<string>();
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var caption = new Label(label);
            caption.style.minWidth = 105f;
            row.Add(caption);
            var currentValue = new Label(values.Length == 0 ? "（未设置）" : string.Join(", ", values));
            currentValue.style.flexGrow = 1f;
            currentValue.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(currentValue);
            var field = new TextField("手动 Tags（逗号分隔）")
            {
                value = string.Join(", ", values),
                isDelayed = true,
            };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt =>
                ApplyActionSetDocumentChange(
                    () => setter(SplitCsv(evt.newValue)),
                    UiRefreshScope.Bindings));

            CharacterActionAuthoringSearchCatalog catalog =
                CharacterActionAuthoringSearchCatalogBuilder.BuildTags(_documentSession, _model);
            var picker = new Button { text = "添加…" };
            picker.style.marginLeft = 6f;
            picker.clicked += () => OpenSearchPicker(
                picker,
                "添加" + label,
                catalog,
                CharacterActionAuthoringSearchCategory.Tag,
                string.Empty,
                allowClear: false,
                selected =>
                {
                    var result = new List<string>(values);
                    if (!string.IsNullOrWhiteSpace(selected) && !result.Contains(selected))
                        result.Add(selected);
                    ApplyActionSetDocumentChange(
                        () => setter(result.ToArray()),
                        UiRefreshScope.Bindings);
                });
            row.Add(picker);
            parent.Add(row);

            var advanced = new Foldout
            {
                text = "高级：手动输入 " + label,
                value = false,
            };
            advanced.Add(field);
            parent.Add(advanced);
        }

        private void OpenSearchPicker(
            VisualElement anchor,
            string title,
            CharacterActionAuthoringSearchCatalog catalog,
            CharacterActionAuthoringSearchCategory category,
            string currentValue,
            bool allowClear,
            Action<string> onSelected)
        {
            CharacterActionAuthoringSearchPickerWindow.ShowPicker(
                ResolveSearchPickerActivatorRect(anchor),
                title,
                catalog,
                category,
                currentValue,
                allowClear,
                onSelected);
        }

        private Rect ResolveSearchPickerActivatorRect(VisualElement anchor)
        {
            Rect bounds = anchor?.worldBound ?? new Rect(0f, 0f, 1f, 1f);
            return new Rect(
                position.x + bounds.x,
                position.y + bounds.y,
                Math.Max(1f, bounds.width),
                Math.Max(1f, bounds.height));
        }
    }
}
