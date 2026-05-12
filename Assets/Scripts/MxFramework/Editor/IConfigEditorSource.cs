using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Editor
{
    public readonly struct ConfigEditorCellPreview
    {
        public ConfigEditorCellPreview(string fieldName, string value, string controlHint)
            : this(fieldName, string.Empty, value, value, controlHint, string.Empty, new string[0])
        {
        }

        public ConfigEditorCellPreview(
            string fieldName,
            string fieldDisplayName,
            string value,
            string displayValue,
            string controlHint,
            string enumId,
            IReadOnlyList<string> options)
        {
            FieldName = fieldName ?? string.Empty;
            FieldDisplayName = fieldDisplayName ?? string.Empty;
            Value = value ?? string.Empty;
            DisplayValue = displayValue ?? string.Empty;
            ControlHint = controlHint ?? string.Empty;
            EnumId = enumId ?? string.Empty;
            Options = options ?? new string[0];
        }

        public string FieldName { get; }
        public string FieldDisplayName { get; }
        public string Value { get; }
        public string DisplayValue { get; }
        public string ControlHint { get; }
        public string EnumId { get; }
        public IReadOnlyList<string> Options { get; }
    }

    public sealed class ConfigEditorRowPreview
    {
        private readonly ConfigEditorCellPreview[] _cells;

        public ConfigEditorRowPreview(int rowId, ConfigEditorCellPreview[] cells)
        {
            RowId = rowId;
            _cells = cells ?? new ConfigEditorCellPreview[0];
        }

        public int RowId { get; }
        public IReadOnlyList<ConfigEditorCellPreview> Cells => _cells;
    }

    public interface IConfigEditorSource
    {
        string Name { get; }
        string SourceType { get; }
        ConfigSchema Schema { get; }
        int RowCount { get; }
        ConfigAuthoringTemplate CreateTemplate();
        ConfigAuthoringReport Validate();
        string CreateTsvPreview(int maxRows);
        string CreateReport();
    }

    public interface IConfigEditorSourceIndexProvider
    {
        ConfigSourceEntry CreateSourceEntry();
        ConfigAuthoringReport Validate(ConfigSourceIndex sourceIndex);
    }

    public interface IConfigEditorTablePreviewProvider
    {
        IReadOnlyList<ConfigEditorRowPreview> CreateRowPreview(int maxRows);
    }

    public interface IConfigEditorEnumProvider
    {
        bool TryGetEnumDomain(string enumId, out ConfigEnumDomain domain);
    }
}
