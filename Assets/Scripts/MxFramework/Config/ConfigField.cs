using System;

namespace MxFramework.Config
{
    public readonly struct ConfigField
    {
        public ConfigField(
            string name,
            ConfigFieldType fieldType,
            string displayName = "",
            string description = "",
            bool required = false,
            Type valueType = null,
            ConfigReferenceRule referenceRule = default,
            string enumId = "")
        {
            Name = name ?? string.Empty;
            FieldType = fieldType;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Required = required;
            ValueType = valueType;
            ReferenceRule = referenceRule;
            EnumId = enumId ?? string.Empty;
        }

        public string Name { get; }
        public ConfigFieldType FieldType { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public bool Required { get; }
        public Type ValueType { get; }
        public ConfigReferenceRule ReferenceRule { get; }
        public string EnumId { get; }
    }
}
