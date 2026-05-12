using System;
using System.Collections.Generic;

namespace MxFramework.Config
{
    public class ConfigSchema
    {
        private readonly List<ConfigField> _fields;
        private readonly List<ConfigReferenceRule> _referenceRules;
        private readonly List<LocaleId> _requiredLocales;

        public ConfigSchema(
            string tableName,
            Type configType,
            string displayName = "",
            string description = "",
            ConfigIdRange idRange = default,
            ConfigStructureKind structureKind = ConfigStructureKind.Table)
        {
            TableName = tableName ?? string.Empty;
            ConfigType = configType;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            IdRange = idRange;
            StructureKind = structureKind;
            _fields = new List<ConfigField>();
            _referenceRules = new List<ConfigReferenceRule>();
            _requiredLocales = new List<LocaleId>();
        }

        public string TableName { get; }
        public Type ConfigType { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public ConfigIdRange IdRange { get; }
        public ConfigStructureKind StructureKind { get; }
        public IReadOnlyList<ConfigField> Fields => _fields;
        public IReadOnlyList<ConfigReferenceRule> ReferenceRules => _referenceRules;
        public IReadOnlyList<LocaleId> RequiredLocales => _requiredLocales;

        public ConfigSchema AddField(ConfigField field)
        {
            _fields.Add(field);
            if (field.ReferenceRule.IsValid)
                _referenceRules.Add(field.ReferenceRule);
            return this;
        }

        public ConfigSchema AddReferenceRule(ConfigReferenceRule rule)
        {
            if (rule.IsValid)
                _referenceRules.Add(rule);
            return this;
        }

        public ConfigSchema RequireLocale(LocaleId locale)
        {
            if (!locale.IsValid)
                return this;

            for (int i = 0; i < _requiredLocales.Count; i++)
            {
                if (_requiredLocales[i].Equals(locale))
                    return this;
            }

            _requiredLocales.Add(locale);
            return this;
        }
    }

    public sealed class ConfigSchema<T> : ConfigSchema where T : IConfigData
    {
        public ConfigSchema(
            string tableName,
            string displayName = "",
            string description = "",
            ConfigIdRange idRange = default,
            ConfigStructureKind structureKind = ConfigStructureKind.Table)
            : base(tableName, typeof(T), displayName, description, idRange, structureKind)
        {
        }
    }
}
