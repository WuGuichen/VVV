using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public sealed class BuffAuthoringDocument
    {
        public string SchemaVersion { get; set; } = "1";
        public string PackageId { get; set; } = string.Empty;
        public List<BuffDefinitionConfig> Buffs { get; set; } = new List<BuffDefinitionConfig>();
        public List<AuraBuffDefinitionConfig> Auras { get; set; } = new List<AuraBuffDefinitionConfig>();
    }

    public sealed class BuffDefinitionConfig
    {
        public int BuffId { get; set; }
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Types { get; set; } = new List<string>();
        public long DefaultDurationFrames { get; set; }
        public int MaxStackCount { get; set; } = 1;
        public int ApplyStackCount { get; set; } = 1;
        public string StackPolicy { get; set; } = "RefreshDuration";
        public bool IsPermanent { get; set; }
        public List<BuffEffectConfig> Effects { get; set; } = new List<BuffEffectConfig>();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuffPresentationConfig Presentation { get; set; }
    }

    public sealed class BuffPresentationConfig
    {
        public string NameKey { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string DescriptionMode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconResourceKey { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Polarity { get; set; } = string.Empty;
        public int? Priority { get; set; }
        public bool ShowOnPlayerHud { get; set; } = true;
        public bool ShowOnTargetHud { get; set; } = true;
        public bool ShowOnHeadPad { get; set; } = true;
    }

    public sealed class BuffEffectConfig
    {
        public int EffectId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public int AttributeId { get; set; }
        public int AddValue { get; set; }
        public int StatusId { get; set; }
    }

    public sealed class AuraBuffDefinitionConfig
    {
        public int AuraId { get; set; }
        public int BuffId { get; set; }
        public float RadiusMeters { get; set; }
        public int LayerMask { get; set; }
        public string RelationFilter { get; set; } = "Any";
        public bool IncludeCaster { get; set; } = true;
    }

    public sealed class BuffAuthoringCompileResult
    {
        public string PackageId { get; set; } = string.Empty;
        public BuffAuthoringValidationReport ValidationReport { get; set; } = new BuffAuthoringValidationReport();
        public BuffDefinitionCatalogDocument BuffDefinitions { get; set; } = new BuffDefinitionCatalogDocument();
        public AuraBuffDefinitionCatalogDocument AuraBuffDefinitions { get; set; } = new AuraBuffDefinitionCatalogDocument();
        public BuffPresentationDefinitionCatalogDocument BuffPresentationDefinitions { get; set; } =
            new BuffPresentationDefinitionCatalogDocument();
    }

    public sealed class BuffAuthoringValidationReport
    {
        public string PackageId { get; set; } = string.Empty;
        public string Status { get; set; } = "Ready";
        public List<BuffAuthoringDiagnostic> Diagnostics { get; set; } = new List<BuffAuthoringDiagnostic>();
        public bool HasBlockingIssues { get; set; }

        public string ToText()
        {
            var lines = new List<string>
            {
                "packageId=" + (PackageId ?? string.Empty),
                "status=" + (Status ?? string.Empty),
                "diagnosticCount=" + Diagnostics.Count
            };
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                BuffAuthoringDiagnostic diagnostic = Diagnostics[i];
                lines.Add(
                    diagnostic.Severity + " "
                    + diagnostic.Code + " "
                    + diagnostic.Path + " - "
                    + diagnostic.Message);
            }

            return string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine;
        }
    }

    public sealed class BuffAuthoringDiagnostic
    {
        public string Severity { get; set; } = "Info";
        public string Code { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class BuffDefinitionCatalogDocument
    {
        public string SchemaVersion { get; set; } = "1";
        public string PackageId { get; set; } = string.Empty;
        public List<BuffDefinitionConfig> Definitions { get; set; } = new List<BuffDefinitionConfig>();
    }

    public sealed class AuraBuffDefinitionCatalogDocument
    {
        public string SchemaVersion { get; set; } = "1";
        public string PackageId { get; set; } = string.Empty;
        public List<AuraBuffDefinitionConfig> Definitions { get; set; } = new List<AuraBuffDefinitionConfig>();
    }

    public sealed class BuffPresentationDefinitionCatalogDocument
    {
        public string SchemaVersion { get; set; } = "1";
        public string PackageId { get; set; } = string.Empty;
        public List<BuffPresentationDefinitionConfig> Definitions { get; set; } =
            new List<BuffPresentationDefinitionConfig>();
    }

    public sealed class BuffPresentationDefinitionConfig
    {
        public int BuffId { get; set; }
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string NameKey { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string DescriptionMode { get; set; } = "Auto";
        public string Description { get; set; } = string.Empty;
        public string IconResourceKey { get; set; } = string.Empty;
        public string Polarity { get; set; } = "Neutral";
        public int Priority { get; set; }
        public bool ShowOnPlayerHud { get; set; } = true;
        public bool ShowOnTargetHud { get; set; } = true;
        public bool ShowOnHeadPad { get; set; } = true;
        public string EffectSummary { get; set; } = string.Empty;
    }
}
