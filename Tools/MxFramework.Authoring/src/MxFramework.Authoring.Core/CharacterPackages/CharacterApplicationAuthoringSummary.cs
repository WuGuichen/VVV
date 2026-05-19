using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class CharacterApplicationCompileBoundary
    {
        public List<string> AuthoringOnly { get; set; } = new List<string>();
        public List<string> RuntimeConfigCandidates { get; set; } = new List<string>();
    }

    public sealed class CharacterApplicationAuthoringSummary
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string AuthoringNote { get; set; } = string.Empty;
        public string CharacterStableId { get; set; } = string.Empty;
        public string BodyProfileStableId { get; set; } = string.Empty;
        public string AttributeProfileStableId { get; set; } = string.Empty;
        public string EquipmentSchemaStableId { get; set; } = string.Empty;
        public List<string> Loadouts { get; set; } = new List<string>();
        public List<string> ResourceKeys { get; set; } = new List<string>();
        public CharacterApplicationCompileBoundary CompileBoundary { get; set; } = new CharacterApplicationCompileBoundary();
    }
}
