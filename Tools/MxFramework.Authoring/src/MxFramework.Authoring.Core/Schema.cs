using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public enum FieldType
    {
        String,
        Integer,
        Float,
        Boolean,
        Enum,
        Reference,
        LocalizedText,
        AssetPath
    }

    public sealed class SchemaField
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public FieldType Type { get; set; }
        public bool Required { get; set; }
        public string EnumId { get; set; } = string.Empty;
        public string ReferenceSource { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupDisplayName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public bool IsList { get; set; }
        public List<string> VisibleWhenBuffTypes { get; set; } = new List<string>();
    }

    public sealed class ConfigSchema
    {
        public string SchemaId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string StructureKind { get; set; } = "Table";
        public List<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }

    public sealed class ReferenceEntry
    {
        public string Source { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }

    public sealed class ReferenceIndex
    {
        public string Source { get; set; } = string.Empty;
        public List<ReferenceEntry> Entries { get; set; } = new List<ReferenceEntry>();
    }

    public sealed class LocalizationEntry
    {
        public string Key { get; set; } = string.Empty;
        public string ZhCN { get; set; } = string.Empty;
        public string EnUS { get; set; } = string.Empty;
    }

    public sealed class ProjectAuthoringManifest
    {
        public string ProjectId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AuthoringVersion { get; set; } = "1.0";
        public string SchemaVersion { get; set; } = "1.0";
        public string GeneratedBy { get; set; } = "MxFramework.Authoring";
        public List<ConfigSchema> Schemas { get; set; } = new List<ConfigSchema>();
        public List<EnumDomain> Enums { get; set; } = new List<EnumDomain>();
        public List<ReferenceIndex> References { get; set; } = new List<ReferenceIndex>();
        public List<AuthoringWorkflow> Workflows { get; set; } = new List<AuthoringWorkflow>();
        public List<LocalizationEntry> Localization { get; set; } = new List<LocalizationEntry>();
        public List<string> AssetWhitelistPrefixes { get; set; } = new List<string>();
    }

    public sealed class EnumOption
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public sealed class EnumDomain
    {
        public string EnumId { get; set; } = string.Empty;
        public bool IsFlags { get; set; }
        public List<EnumOption> Options { get; set; } = new List<EnumOption>();
    }
}
