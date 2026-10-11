using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MxFramework.Authoring
{
    /// <summary>Serializable workspace inventory shared by workspace and mutation commands.</summary>
    /// <remarks>LowFreqAlloc: authoring-only projection and ownership lists.</remarks>
    public sealed class ProjectileAuthoringWorkspaceProjection
    {
        public string SchemaVersion { get; set; } = "1";
        public string PackageId { get; set; } = string.Empty;
        public string SourceRoot { get; set; } = string.Empty;
        public string WorkspaceHash { get; set; } = string.Empty;
        public int SourceCount { get; set; }
        public int DefinitionCount { get; set; }
        public int RequestCount { get; set; }
        public int PresentationBindingCount { get; set; }
        public List<ProjectileAuthoringWorkspaceSource> Sources { get; set; } = new List<ProjectileAuthoringWorkspaceSource>();
        public List<ProjectileAuthoringWorkspaceDefinition> Definitions { get; set; } = new List<ProjectileAuthoringWorkspaceDefinition>();
        public List<ProjectileAuthoringWorkspaceRequest> Requests { get; set; } = new List<ProjectileAuthoringWorkspaceRequest>();
        public List<ProjectileAuthoringWorkspaceBinding> Bindings { get; set; } = new List<ProjectileAuthoringWorkspaceBinding>();
        public List<ProjectileAuthoringWorkspaceGeneratedOnly> GeneratedOnly { get; set; } = new List<ProjectileAuthoringWorkspaceGeneratedOnly>();
        public List<ProjectileAuthoringDiagnostic> Diagnostics { get; set; } = new List<ProjectileAuthoringDiagnostic>();
        public List<string> KnownSocketIds { get; set; } = new List<string>();
    }

    public sealed class ProjectileAuthoringWorkspaceSource { public string StableId { get; set; } = string.Empty; public string RelativePath { get; set; } = string.Empty; public string ExpectedSha256 { get; set; } = string.Empty; public string State { get; set; } = "Source"; }
    public sealed class ProjectileAuthoringWorkspaceDefinition
    {
        public string StableId { get; set; } = string.Empty; public int DefinitionId { get; set; }
        public string Motion { get; set; } = string.Empty; public string Impact { get; set; } = string.Empty;
        public List<string> RequestIds { get; set; } = new List<string>(); public bool Shared { get; set; } public bool HasPresentation { get; set; }
        public List<string> SourcePaths { get; set; } = new List<string>();
        public List<string> PresentationSourcePaths { get; set; } = new List<string>();
        public ProjectileDefinitionConfig Detail { get; set; }
        public string RawJson { get; set; } = string.Empty;
    }
    public sealed class ProjectileAuthoringWorkspaceRequest
    {
        public string StableId { get; set; } = string.Empty; public string DefinitionStableId { get; set; } = string.Empty; public string SourcePath { get; set; } = string.Empty;
        public ProjectileSpawnProfileConfig Detail { get; set; }
        public string RawJson { get; set; } = string.Empty;
    }
    public sealed class ProjectileAuthoringWorkspaceBinding
    {
        public int ProjectileDefinitionId { get; set; }
        public string DefinitionStableId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public bool LegacyV1 { get; set; }
        public ProjectilePresentationBindingConfig Detail { get; set; }
        public string RawJson { get; set; } = string.Empty;
    }
    public sealed class ProjectileAuthoringWorkspaceGeneratedOnly
    {
        public string StableId { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public bool Complete { get; set; }
        public int DefinitionId { get; set; } public string Motion { get; set; } = string.Empty; public string Impact { get; set; } = string.Empty;
        public List<string> Diagnostics { get; set; } = new List<string>();
    }

    public static class ProjectileAuthoringWorkspaceProjectionBuilder
    {
        public static ProjectileAuthoringWorkspaceProjection Build(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringCompileResult compiled)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (compiled == null) throw new ArgumentNullException(nameof(compiled));
            var output = new ProjectileAuthoringWorkspaceProjection
            {
                PackageId = workspace.AbilityDocument.PackageId ?? string.Empty,
                SourceRoot = Normalize(workspace.ProjectileSourceRoot),
                Diagnostics = compiled.Diagnostics.Select(CloneDiagnostic).ToList(),
                KnownSocketIds = workspace.AbilityDocument.KnownSocketIds == null
                    ? new List<string>()
                    : workspace.AbilityDocument.KnownSocketIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList(),
                GeneratedOnly = workspace.GeneratedOnly.Select(value => new ProjectileAuthoringWorkspaceGeneratedOnly
                {
                    StableId = value.StableId ?? string.Empty, Status = value.Status ?? string.Empty, Complete = value.Complete,
                    DefinitionId = value.DefinitionId, Motion = value.Definition?.Motion?.Kind ?? string.Empty,
                    Impact = value.Definition?.Impact?.PolicyKind ?? string.Empty, Diagnostics = new List<string>(value.Diagnostics),
                }).ToList(),
            };
            output.Sources = workspace.SourceDocuments.OrderBy(value => value.RelativePath, StringComparer.Ordinal).Select(source => new ProjectileAuthoringWorkspaceSource
            {
                StableId = source.SourceStableId ?? string.Empty, RelativePath = Normalize(source.RelativePath), ExpectedSha256 = source.Sha256 ?? string.Empty,
            }).ToList();
            output.Requests = workspace.SourceDocuments.Where(source => source.Projection?.SpawnProfile != null).Select(source =>
            {
                string raw = CloneSubtree(source, "spawnProfile");
                return new ProjectileAuthoringWorkspaceRequest
                {
                    StableId = source.Projection.SpawnProfile.StableId ?? string.Empty,
                    DefinitionStableId = source.Projection.SpawnProfile.ProjectileStableId ?? string.Empty,
                    SourcePath = Normalize(source.RelativePath),
                    Detail = DeserializeRaw<ProjectileSpawnProfileConfig>(raw) ?? Clone(source.Projection.SpawnProfile),
                    RawJson = raw,
                };
            }).OrderBy(value => value.StableId, StringComparer.Ordinal).ToList();
            output.Bindings = workspace.SourceDocuments.Where(source => source.Projection?.PresentationBinding != null).Select(source =>
            {
                string raw = CloneSubtree(source, "presentationBinding");
                ProjectilePresentationBindingConfig sourceBinding = source.Projection.PresentationBinding;
                ProjectilePresentationBindingConfig compiledBinding = compiled.PresentationBindings?.Bindings?.FirstOrDefault(value =>
                    value.ProjectileDefinitionId == sourceBinding.ProjectileDefinitionId);
                return new ProjectileAuthoringWorkspaceBinding
                {
                    ProjectileDefinitionId = sourceBinding.ProjectileDefinitionId,
                    DefinitionStableId = workspace.ReferenceIndex.DefinitionsByNumericId.TryGetValue(sourceBinding.ProjectileDefinitionId, out ProjectileDefinitionConfig definition) ? definition.StableId ?? string.Empty : string.Empty,
                    SourcePath = Normalize(source.RelativePath),
                    LegacyV1 = raw.IndexOf("\"prefabResource\"", StringComparison.Ordinal) >= 0,
                    Detail = Clone(compiledBinding ?? sourceBinding),
                    RawJson = raw,
                };
            }).OrderBy(value => value.ProjectileDefinitionId).ThenBy(value => value.SourcePath, StringComparer.Ordinal).ToList();
            output.Definitions = compiled.Definitions.Definitions.OrderBy(value => value.StableId, StringComparer.Ordinal).Select(definition =>
            {
                List<string> requests = workspace.ReferenceIndex.RequestIdsByDefinitionStableId.TryGetValue(definition.StableId, out IReadOnlyList<string> ids) ? ids.ToList() : new List<string>();
                List<string> definitionSources = workspace.SourceDocuments.Where(source => source.Projection?.Definition != null && string.Equals(source.Projection.Definition.StableId, definition.StableId, StringComparison.Ordinal)).Select(source => Normalize(source.RelativePath)).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
                List<string> presentationSources = workspace.SourceDocuments.Where(source => source.Projection?.PresentationBinding?.ProjectileDefinitionId == definition.DefinitionId).Select(source => Normalize(source.RelativePath)).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
                string raw = CloneDefinitionRaw(workspace, definition.StableId);
                ProjectileDefinitionConfig detail = DeserializeRaw<ProjectileDefinitionConfig>(raw) ?? Clone(definition);
                if (detail?.Collision != null)
                    detail.Collision.LayerMask = definition.Collision?.LayerMask ?? 0;
                if (detail?.Impact != null)
                    detail.Impact.AreaLayerMask = definition.Impact?.AreaLayerMask ?? 0;
                return new ProjectileAuthoringWorkspaceDefinition
                {
                    StableId = definition.StableId ?? string.Empty, DefinitionId = definition.DefinitionId,
                    Motion = definition.Motion?.Kind ?? string.Empty, Impact = definition.Impact?.PolicyKind ?? string.Empty,
                    RequestIds = requests, Shared = requests.Count > 1, HasPresentation = presentationSources.Count > 0,
                    SourcePaths = definitionSources, PresentationSourcePaths = presentationSources,
                    Detail = detail,
                    RawJson = raw,
                };
            }).ToList();
            output.SourceCount = output.Sources.Count; output.DefinitionCount = output.Definitions.Count;
            output.RequestCount = output.Requests.Count; output.PresentationBindingCount = output.Bindings.Count;
            output.WorkspaceHash = ProjectileAuthoringHashes.ComputeSha256(new UTF8Encoding(false).GetBytes(string.Join("\n", output.Sources.Select(value => value.RelativePath + ":" + value.ExpectedSha256))));
            return output;
        }

        private static ProjectileAuthoringDiagnostic CloneDiagnostic(ProjectileAuthoringDiagnostic value) => new ProjectileAuthoringDiagnostic
        {
            Code = value.Code, Severity = value.Severity, Message = value.Message, SourcePath = Normalize(value.SourcePath), ObjectPath = value.ObjectPath, FieldPath = value.FieldPath,
            RelatedStableIds = value.RelatedStableIds == null ? new List<string>() : new List<string>(value.RelatedStableIds), SuggestedFix = value.SuggestedFix,
        };
        private static string CloneSubtree(ProjectileAuthoringSourceDocument source, string property)
        {
            if (source?.PreservedJson != null && source.PreservedJson.RootElement.TryGetProperty(property, out JsonElement value))
                return value.GetRawText();
            return string.Empty;
        }
        private static string CloneDefinitionRaw(ProjectileAuthoringWorkspace workspace, string stableId)
        {
            ProjectileAuthoringSourceDocument source = workspace.SourceDocuments.FirstOrDefault(value => value.Projection?.Definition != null && string.Equals(value.Projection.Definition.StableId, stableId, StringComparison.Ordinal));
            return CloneSubtree(source, "definition");
        }
        private static T Clone<T>(T value)
        {
            if (value == null) return default;
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
        }
        private static T DeserializeRaw<T>(string raw) where T : class
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        private static string Normalize(string value) => (value ?? string.Empty).Replace('\\', '/').TrimStart('/');
    }
}
