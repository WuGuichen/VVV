using System;

namespace MxFramework.Config
{
    public readonly struct ConfigReferenceRule
    {
        public ConfigReferenceRule(
            string fieldName,
            Type targetType,
            bool required = true,
            ConfigValidationSeverity severity = ConfigValidationSeverity.Error)
        {
            FieldName = fieldName ?? string.Empty;
            TargetType = targetType;
            TargetSchemaName = targetType != null ? targetType.Name : string.Empty;
            TargetStructureKind = ConfigStructureKind.Table;
            TargetKeyField = "Id";
            Required = required;
            Severity = severity;
        }

        public ConfigReferenceRule(
            string fieldName,
            string targetSchemaName,
            ConfigStructureKind targetStructureKind,
            string targetKeyField = "Id",
            bool required = true,
            ConfigValidationSeverity severity = ConfigValidationSeverity.Error)
        {
            FieldName = fieldName ?? string.Empty;
            TargetType = null;
            TargetSchemaName = targetSchemaName ?? string.Empty;
            TargetStructureKind = targetStructureKind;
            TargetKeyField = string.IsNullOrWhiteSpace(targetKeyField) ? "Id" : targetKeyField;
            Required = required;
            Severity = severity;
        }

        public string FieldName { get; }
        public Type TargetType { get; }
        public string TargetSchemaName { get; }
        public ConfigStructureKind TargetStructureKind { get; }
        public string TargetKeyField { get; }
        public bool Required { get; }
        public ConfigValidationSeverity Severity { get; }
        public bool HasRuntimeTarget => TargetType != null;
        public bool IsValid => !string.IsNullOrWhiteSpace(FieldName) && (TargetType != null || !string.IsNullOrWhiteSpace(TargetSchemaName));

        public string GetTargetDisplayName()
        {
            if (!IsValid)
                return string.Empty;

            string schemaName = !string.IsNullOrEmpty(TargetSchemaName)
                ? TargetSchemaName
                : TargetType.Name;

            return TargetStructureKind + ":" + schemaName + "." + TargetKeyField;
        }
    }
}
