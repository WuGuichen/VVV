using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    /// <summary>
    /// Describes one stable, project-owned option shown by an Editor authoring
    /// reference field.
    /// </summary>
    /// <typeparam name="TValue">The persisted value type.</typeparam>
    /// <remarks>
    /// AllocByDesign. This contract is Editor-only and intended for low-frequency
    /// inspector construction.
    /// </remarks>
    public readonly struct MxAuthoringReferenceOption<TValue>
    {
        public MxAuthoringReferenceOption(
            string key,
            TValue value,
            string displayName,
            string summary = null,
            string tooltip = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A stable option key is required.", nameof(key));

            Key = key.Trim();
            Value = value;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? Key
                : displayName.Trim();
            Summary = string.IsNullOrWhiteSpace(summary)
                ? string.Empty
                : summary.Trim();
            Tooltip = string.IsNullOrWhiteSpace(tooltip)
                ? string.Empty
                : tooltip.Trim();
        }

        public string Key { get; }

        public TValue Value { get; }

        public string DisplayName { get; }

        public string Summary { get; }

        public string Tooltip { get; }
    }

    /// <summary>
    /// Builds a compact Editor-only field for selecting values from a
    /// business-owned reference catalog.
    /// </summary>
    /// <remarks>
    /// AllocByDesign. Creates UI Toolkit elements and temporary display lists
    /// only while an Editor inspector is being built.
    /// </remarks>
    public static class MxAuthoringReferenceField
    {
        public const string RootClassName = "mx-authoring-reference-field";
        public const string EmptyClassName =
            "mx-authoring-reference-field--empty";
        public const string SingleClassName =
            "mx-authoring-reference-field--single";
        public const string MultipleClassName =
            "mx-authoring-reference-field--multiple";
        public const string UnknownClassName =
            "mx-authoring-reference-field--unknown";

        public const string LabelElementName =
            "mx-authoring-reference-label";
        public const string ValueElementName =
            "mx-authoring-reference-value";
        public const string ContentElementName =
            "mx-authoring-reference-content";
        public const string SummaryElementName =
            "mx-authoring-reference-summary";
        public const string DiagnosticElementName =
            "mx-authoring-reference-diagnostic";

        /// <summary>
        /// Creates a reference field without changing <paramref name="currentValue"/>
        /// until the user selects a registered option.
        /// </summary>
        /// <typeparam name="TValue">The persisted value type.</typeparam>
        /// <param name="label">User-facing semantic field name.</param>
        /// <param name="currentValue">The current persisted value.</param>
        /// <param name="availableOptions">
        /// Candidates built directly from the owning business catalog.
        /// </param>
        /// <param name="onValueChanged">
        /// Invoked only after the user explicitly selects a different valid value.
        /// </param>
        /// <param name="comparer">Optional value comparer.</param>
        /// <param name="name">Optional stable UI Toolkit element name.</param>
        /// <param name="emptyDiagnostic">
        /// Diagnostic used when the business catalog has no valid candidates.
        /// </param>
        /// <param name="unknownValueFormatter">
        /// Optional formatter for an unregistered current value.
        /// </param>
        /// <param name="unknownDiagnostic">
        /// Compatibility warning used when the current value is unregistered.
        /// </param>
        /// <returns>A complete compact UI Toolkit field row.</returns>
        /// <remarks>
        /// AllocByDesign. The returned controls are intended for Editor inspectors,
        /// never Runtime or per-frame paths.
        /// </remarks>
        public static VisualElement Create<TValue>(
            string label,
            TValue currentValue,
            IReadOnlyList<MxAuthoringReferenceOption<TValue>> availableOptions,
            Action<TValue> onValueChanged,
            IEqualityComparer<TValue> comparer = null,
            string name = null,
            string emptyDiagnostic = null,
            Func<TValue, string> unknownValueFormatter = null,
            string unknownDiagnostic = null)
        {
            comparer ??= EqualityComparer<TValue>.Default;
            int optionCount = availableOptions?.Count ?? 0;
            int selectedIndex = FindOption(
                availableOptions,
                currentValue,
                comparer);

            var root = new VisualElement
            {
                name = string.IsNullOrWhiteSpace(name)
                    ? RootClassName
                    : name.Trim()
            };
            root.AddToClassList(RootClassName);
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.FlexStart;
            root.style.marginTop = 2f;
            root.style.marginBottom = 2f;

            var fieldLabel = new Label(label ?? string.Empty)
            {
                name = LabelElementName
            };
            fieldLabel.style.width = 92f;
            fieldLabel.style.minWidth = 92f;
            fieldLabel.style.paddingTop = 3f;
            root.Add(fieldLabel);

            var content = new VisualElement
            {
                name = ContentElementName
            };
            content.style.flexGrow = 1f;
            content.style.flexShrink = 1f;
            root.Add(content);

            if (optionCount == 0)
            {
                root.AddToClassList(EmptyClassName);
                content.Add(CreateDiagnostic(
                    string.IsNullOrWhiteSpace(emptyDiagnostic)
                        ? "没有可用的正式目录候选。"
                        : emptyDiagnostic.Trim(),
                    HelpBoxMessageType.Warning));
                return root;
            }

            bool isUnknown = selectedIndex < 0;
            if (isUnknown)
                root.AddToClassList(UnknownClassName);

            if (optionCount == 1)
            {
                root.AddToClassList(SingleClassName);
                MxAuthoringReferenceOption<TValue> option =
                    availableOptions[0];
                string text = isUnknown
                    ? FormatUnknownValue(
                        currentValue,
                        unknownValueFormatter)
                    : option.DisplayName;
                var value = new Label(text)
                {
                    name = ValueElementName,
                    tooltip = isUnknown
                        ? BuildUnknownDiagnostic(unknownDiagnostic)
                        : option.Tooltip
                };
                value.style.paddingTop = 3f;
                value.style.paddingBottom = 3f;
                content.Add(value);

                if (!isUnknown)
                    AddSummary(content, option.Summary);
                else
                    content.Add(CreateDiagnostic(
                        BuildUnknownDiagnostic(unknownDiagnostic),
                        HelpBoxMessageType.Warning));
                return root;
            }

            root.AddToClassList(MultipleClassName);
            var labels = BuildLabels(availableOptions);
            var field = new DropdownField(
                labels,
                selectedIndex >= 0 ? selectedIndex : 0)
            {
                name = ValueElementName
            };
            field.style.flexGrow = 1f;
            if (selectedIndex >= 0)
            {
                field.tooltip = availableOptions[selectedIndex].Tooltip;
            }
            else
            {
                field.SetValueWithoutNotify(
                    FormatUnknownValue(
                        currentValue,
                        unknownValueFormatter));
                field.tooltip = BuildUnknownDiagnostic(unknownDiagnostic);
            }
            content.Add(field);

            Label summary = AddSummary(
                content,
                selectedIndex >= 0
                    ? availableOptions[selectedIndex].Summary
                    : string.Empty);
            if (isUnknown)
            {
                content.Add(CreateDiagnostic(
                    BuildUnknownDiagnostic(unknownDiagnostic),
                    HelpBoxMessageType.Warning));
            }

            field.RegisterValueChangedCallback(
                change =>
                {
                    int nextIndex = labels.IndexOf(change.newValue);
                    if (nextIndex < 0
                        || nextIndex >= availableOptions.Count)
                    {
                        return;
                    }

                    MxAuthoringReferenceOption<TValue> option =
                        availableOptions[nextIndex];
                    field.tooltip = option.Tooltip;
                    if (summary != null)
                    {
                        summary.text = option.Summary;
                        summary.style.display =
                            string.IsNullOrWhiteSpace(option.Summary)
                                ? DisplayStyle.None
                                : DisplayStyle.Flex;
                    }

                    if (!comparer.Equals(option.Value, currentValue))
                        onValueChanged?.Invoke(option.Value);
                });
            return root;
        }

        private static int FindOption<TValue>(
            IReadOnlyList<MxAuthoringReferenceOption<TValue>> options,
            TValue currentValue,
            IEqualityComparer<TValue> comparer)
        {
            for (int i = 0; options != null && i < options.Count; i++)
            {
                if (comparer.Equals(options[i].Value, currentValue))
                    return i;
            }

            return -1;
        }

        private static List<string> BuildLabels<TValue>(
            IReadOnlyList<MxAuthoringReferenceOption<TValue>> options)
        {
            var labels = new List<string>(options.Count);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < options.Count; i++)
            {
                string displayName = options[i].DisplayName;
                counts.TryGetValue(displayName, out int count);
                counts[displayName] = count + 1;
            }

            for (int i = 0; i < options.Count; i++)
            {
                MxAuthoringReferenceOption<TValue> option = options[i];
                labels.Add(counts[option.DisplayName] > 1
                    ? option.DisplayName + " · " + option.Key
                    : option.DisplayName);
            }
            return labels;
        }

        private static Label AddSummary(
            VisualElement content,
            string summaryText)
        {
            var summary = new Label(summaryText ?? string.Empty)
            {
                name = SummaryElementName
            };
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.opacity = 0.72f;
            summary.style.marginTop = 2f;
            summary.style.display = string.IsNullOrWhiteSpace(summaryText)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            content.Add(summary);
            return summary;
        }

        private static HelpBox CreateDiagnostic(
            string message,
            HelpBoxMessageType messageType)
        {
            return new HelpBox(message, messageType)
            {
                name = DiagnosticElementName
            };
        }

        private static string FormatUnknownValue<TValue>(
            TValue value,
            Func<TValue, string> formatter)
        {
            string text = formatter?.Invoke(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture);
            }
            return string.IsNullOrWhiteSpace(text)
                ? "未登记值"
                : text.Trim();
        }

        private static string BuildUnknownDiagnostic(string diagnostic)
        {
            return string.IsNullOrWhiteSpace(diagnostic)
                ? "当前值不在正式目录中，原值已保留；只有明确选择有效项后才会替换。"
                : diagnostic.Trim();
        }
    }
}
