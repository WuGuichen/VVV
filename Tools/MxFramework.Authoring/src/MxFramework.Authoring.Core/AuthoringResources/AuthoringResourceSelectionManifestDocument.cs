using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class AuthoringResourceSelectionManifestDocument
    {
        public string Format { get; set; } = "mx.authoringResourceSelections.v1";
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string ConsumerKind { get; set; } = string.Empty;
        public string ConsumerStableId { get; set; } = string.Empty;
        public List<AuthoringResourceSelectionCompileInput> Selections { get; set; } = new List<AuthoringResourceSelectionCompileInput>();
    }

    public sealed class AuthoringResourceSelectionCompileInput
    {
        public string SourceConfigKind { get; set; } = string.Empty;
        public string SourceStableId { get; set; } = string.Empty;
        public string SourceField { get; set; } = string.Empty;
        public AuthoringResourceFieldSpec FieldSpec { get; set; } = new AuthoringResourceFieldSpec();
        public AuthoringResourceConsumerContext Context { get; set; } = new AuthoringResourceConsumerContext();
        public AuthoringResourceSelectionRef Selection { get; set; } = new AuthoringResourceSelectionRef();
    }
}
