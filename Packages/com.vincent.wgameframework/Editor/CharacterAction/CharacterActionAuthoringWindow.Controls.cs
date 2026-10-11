using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private VisualElement AddCard(VisualElement parent, string title)
        {
            var card = new VisualElement();
            card.AddToClassList("ca-card");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("ca-card__title");
            card.Add(titleLabel);
            parent.Add(card);
            return card;
        }

        private void AddButtonRow(VisualElement parent, params (string label, Action action)[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 4f;
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = new Button(buttons[i].action) { text = buttons[i].label };
                button.style.marginRight = 4f;
                button.style.marginBottom = 2f;
                row.Add(button);
            }

            parent.Add(row);
        }

        private static VisualElement CreateToolbarSpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("ca-toolbar-spacer");
            spacer.style.flexGrow = 1f;
            return spacer;
        }

        private void AddTextField(VisualElement parent, string label, string value, Action<string> setter)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt => ApplyFieldChange(() => setter(evt.newValue ?? string.Empty)));
            parent.Add(field);
        }

        private void AddReferenceTextField(VisualElement parent, string label, string value, List<string> choices, Action<string> setter)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;

            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
            };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt => ApplyFieldChange(() => setter(evt.newValue ?? string.Empty)));
            row.Add(field);

            List<string> popupChoices = BuildReferenceChoices(value, choices);
            if (popupChoices.Count > 0)
            {
                var menuLabels = new List<string>(popupChoices.Count);
                for (int i = 0; i < popupChoices.Count; i++)
                    menuLabels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(popupChoices[i]));

                var popup = new PopupField<string>("", menuLabels, menuLabels[0]);
                popup.style.minWidth = 220f;
                popup.RegisterValueChangedCallback(evt =>
                {
                    int index = menuLabels.IndexOf(evt.newValue);
                    if (index < 0 || index >= popupChoices.Count)
                        return;
                    ApplyFieldChange(() => setter(popupChoices[index] ?? string.Empty));
                });
                row.Add(popup);
            }

            parent.Add(row);
        }

        private void AddChoiceField(
            VisualElement parent,
            string label,
            string value,
            CharacterActionPreviewBindingChoice[] choices,
            Action<string> setter,
            UiRefreshScope refreshScope = UiRefreshScope.Content)
        {
            choices = BuildChoiceList(value, choices);
            var labels = new List<string>(choices.Length);
            int selectedIndex = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                string safeLabel = CharacterActionAuthoringUiLabels.ToMenuSafeLabel(choices[i].Label);
                labels.Add(safeLabel);
                if (string.Equals(choices[i].Value, value ?? string.Empty, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            var field = new PopupField<string>(label, labels, selectedIndex);
            field.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index < 0 || index >= choices.Length)
                    return;

                ApplyStructuralChange(() => setter(choices[index].Value), refreshScope);
            });
            parent.Add(field);
        }

        private void AddCsvField(VisualElement parent, string label, List<string> values)
        {
            values = values ?? new List<string>();
            var field = new TextField(label)
            {
                value = string.Join(", ", values),
                multiline = true,
                isDelayed = true,
            };
            field.AddToClassList("ca-csv-field");
            field.RegisterValueChangedCallback(evt =>
                ApplyFieldChange(() => ReplaceList(values, SplitCsv(evt.newValue))));
            parent.Add(field);
        }

        private void AddIntegerField(VisualElement parent, string label, int value, Action<int> setter)
        {
            var field = new IntegerField(label)
            {
                value = value,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt => ApplyFieldChange(() => setter(evt.newValue)));
            parent.Add(field);
        }

        private void AddFloatField(VisualElement parent, string label, float value, Action<float> setter)
        {
            var field = new FloatField(label)
            {
                value = value,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt => ApplyFieldChange(() => setter(evt.newValue)));
            parent.Add(field);
        }

        private void AddToggle(VisualElement parent, string label, bool value, Action<bool> setter)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => ApplyFieldChange(() => setter(evt.newValue)));
            parent.Add(field);
        }

        private void AddEnumPopup<TEnum>(VisualElement parent, string label, string value, Action<string> setter)
            where TEnum : struct
        {
            string[] rawNames = Enum.GetNames(typeof(TEnum));
            var choices = new List<string>(rawNames.Length);
            var rawValues = new List<string>(rawNames.Length);
            for (int i = 0; i < rawNames.Length; i++)
            {
                rawValues.Add(rawNames[i]);
                choices.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(
                    CharacterActionAuthoringText.EnumDisplayLabel(typeof(TEnum), rawNames[i])));
            }

            value = string.IsNullOrWhiteSpace(value) ? rawValues[0] : value;
            int selectedMenuIndex = rawValues.IndexOf(value);
            if (selectedMenuIndex < 0)
            {
                rawValues.Add(value);
                choices.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(
                    CharacterActionAuthoringText.EnumDisplayLabel(typeof(TEnum), value)));
                selectedMenuIndex = choices.Count - 1;
            }

            var field = new PopupField<string>(label, choices, choices[selectedMenuIndex]);
            field.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index < 0 || index >= rawValues.Count)
                    return;
                ApplyFieldChange(() => setter(rawValues[index]));
            });
            parent.Add(field);
        }

        private static TextField CreateTextArea(string label, string value, bool compact = false)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                multiline = true,
                isReadOnly = true,
            };
            field.AddToClassList(compact ? "ca-json-preview" : string.Empty);
            if (!compact)
                field.style.minHeight = 140f;
            return field;
        }

        private static string[] SplitCsv(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            string[] raw = text.Split(',');
            var values = new List<string>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                string item = raw[i].Trim();
                if (!string.IsNullOrEmpty(item))
                    values.Add(item);
            }

            return values.ToArray();
        }

        private static void ReplaceList(List<string> list, string[] values)
        {
            list.Clear();
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (!list.Contains(value))
                    list.Add(value);
            }
        }

        private static List<string> BuildReferenceChoices(string value, List<string> choices)
        {
            var result = new List<string>();
            value = value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);

            choices = choices ?? new List<string>();
            for (int i = 0; i < choices.Count; i++)
            {
                string choice = choices[i];
                if (string.IsNullOrWhiteSpace(choice) || result.Contains(choice))
                    continue;
                result.Add(choice);
            }

            return result;
        }

        private static CharacterActionPreviewBindingChoice[] BuildChoiceList(
            string value,
            CharacterActionPreviewBindingChoice[] choices)
        {
            value = value ?? string.Empty;
            choices = choices ?? Array.Empty<CharacterActionPreviewBindingChoice>();
            var result = new List<CharacterActionPreviewBindingChoice>();
            bool hasCurrent = string.IsNullOrWhiteSpace(value);
            for (int i = 0; i < choices.Length; i++)
            {
                CharacterActionPreviewBindingChoice choice = choices[i];
                if (string.Equals(choice.Value, value, StringComparison.Ordinal))
                    hasCurrent = true;
                result.Add(choice);
            }

            if (!hasCurrent)
                result.Insert(0, new CharacterActionPreviewBindingChoice(value, value + "  [missing]"));
            if (result.Count == 0)
                result.Add(new CharacterActionPreviewBindingChoice(string.Empty, "No generated choices"));
            return result.ToArray();
        }
    }
}
