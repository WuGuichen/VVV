using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    /// <summary>Versioned request for a pure projectile authoring mutation plan.</summary>
    /// <remarks>LowFreqAlloc: authoring commands allocate cloned configuration graphs.</remarks>
    public sealed class ProjectileAuthoringMutationRequest
    {
        public string SchemaVersion { get; set; } = "1";
        public string Kind { get; set; } = string.Empty;
        public string SourceStableId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string DefinitionStableId { get; set; } = string.Empty;
        public int DefinitionId { get; set; }
        public bool CreateRequest { get; set; } = true;
        public bool CopyPresentation { get; set; } = true;
        public bool ConfirmDestructive { get; set; }
        public bool PlanOnly { get; set; }
        public bool Confirmed { get; set; }
        public string ConfirmationToken { get; set; } = string.Empty;
        public string WorkspaceToken { get; set; } = string.Empty;
        public string GeneratedOnlyStableId { get; set; } = string.Empty;
        public List<string> AuthorizedConsumerPaths { get; set; } = new List<string>();
        public List<int> ReservedDefinitionIds { get; set; } = new List<int>();
        public List<int> DependencyDefinitionIds { get; set; } = new List<int>();
        public ProjectileDefinitionConfig GeneratedDefinition { get; set; }
        public ProjectileSpawnProfileConfig GeneratedSpawnProfile { get; set; }
        public ProjectilePresentationBindingConfig GeneratedPresentationBinding { get; set; }
        public string PresentationSlotKind { get; set; } = string.Empty;
        public AbilityResourceKeyConfig PresentationResource { get; set; }
        public string PresentationOrientation { get; set; } = string.Empty;
        public ProjectileVector3Config PresentationLocalOffset { get; set; } = new ProjectileVector3Config();
        public int PresentationLifetimeFramesOverride { get; set; }
        public bool SynchronizeVfxContractLifetime { get; set; }
        public string PresentationExitKind { get; set; } = string.Empty;
        public string PresentationExitMode { get; set; } = string.Empty;
        public List<ProjectileAuthoringFieldEdit> FieldEdits { get; set; } = new List<ProjectileAuthoringFieldEdit>();
    }

    /// <summary>Versioned ordered mutation sequence replayed against one immutable base workspace.</summary>
    /// <remarks>LowFreqAlloc: authoring-only command lists and composite draft construction.</remarks>
    public sealed class ProjectileAuthoringMutationSequenceRequest
    {
        public string SchemaVersion { get; set; } = "1";
        public List<ProjectileAuthoringMutationRequest> Commands { get; set; } = new List<ProjectileAuthoringMutationRequest>();
    }

    /// <summary>Describes consumer and shared-definition impact before a mutation is applied.</summary>
    /// <remarks>LowFreqAlloc: lists are built for interactive diagnostics.</remarks>
    public sealed class ProjectileAuthoringReferenceImpact
    {
        public string StableId { get; set; } = string.Empty;
        public List<string> RequestStableIds { get; set; } = new List<string>();
        public List<string> ConsumerPaths { get; set; } = new List<string>();
        public List<string> AuthorizationRequiredPaths { get; set; } = new List<string>();
        public bool Blocking { get; set; }
        public List<string> BlockingReasons { get; set; } = new List<string>();
        public string ConfirmationToken { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> FieldPaths { get; set; } = new List<string>();
        public string WorkspaceToken { get; set; } = string.Empty;
    }

    /// <summary>Result of a pure mutation plan. It never writes package or generated files.</summary>
    /// <remarks>LowFreqAlloc: contains a complete apply draft and diagnostics.</remarks>
    public sealed class ProjectileAuthoringMutationResult
    {
        public string SchemaVersion { get; set; } = "1";
        public string Status { get; set; } = "Blocked";
        public ProjectileAuthoringDraftBundle Draft { get; set; } = new ProjectileAuthoringDraftBundle();
        public ProjectileAuthoringReferenceImpact Impact { get; set; } = new ProjectileAuthoringReferenceImpact();
        public List<ProjectileAuthoringDirtySource> DirtySources { get; set; } = new List<ProjectileAuthoringDirtySource>();
        public List<ProjectileAuthoringDiagnostic> Diagnostics { get; set; } = new List<ProjectileAuthoringDiagnostic>();
        public ProjectileAuthoringWorkspaceProjection ProjectedWorkspace { get; set; }
    }

    /// <summary>One package-relative source operation produced by a mutation.</summary>
    public sealed class ProjectileAuthoringDirtySource
    {
        public string RelativePath { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
    }

    /// <summary>Builds guarded CRUD drafts while leaving all real files unchanged.</summary>
    public static class ProjectileAuthoringMutationPlanner
    {
        /// <summary>Plans a mutation against a loaded package workspace.</summary>
        /// <remarks>LowFreqAlloc: serializes cloned documents and scans authoring consumers.</remarks>
        public static ProjectileAuthoringMutationResult Plan(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, JsonSerializerOptions options)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (request == null) throw new ArgumentNullException(nameof(request));
            options ??= new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var result = new ProjectileAuthoringMutationResult();
            if (!string.Equals(request.SchemaVersion, "1", StringComparison.Ordinal))
                return Block(result, "PROJAUTH_MUTATION_SCHEMA", "Only mutation schemaVersion 1 is supported.");

            try
            {
                switch (request.Kind ?? string.Empty)
                {
                    case "CreateDefinition": CreateDefinition(workspace, request, options, result); break;
                    case "CreateRequestForExistingDefinition": CreateRequest(workspace, request, options, result); break;
                    case "DuplicateDefinition": DuplicateDefinition(workspace, request, options, result); break;
                    case "DuplicateRequest": DuplicateRequest(workspace, request, options, result); break;
                    case "RenameRequest": RenameRequest(workspace, request, options, result); break;
                    case "RenameDefinition": RenameDefinition(workspace, request, options, result); break;
                    case "DeleteRequest": DeleteRequest(workspace, request, result); break;
                    case "DeleteDefinition": DeleteDefinition(workspace, request, result); break;
                    case "AdoptGeneratedOnly": AdoptGenerated(workspace, request, options, result); break;
                    case "EditDefinitionFields": ProjectileAuthoringFieldEditor.EditDefinition(workspace, request, result); break;
                    case "EditRequestFields": ProjectileAuthoringFieldEditor.EditRequest(workspace, request, result); break;
                    case "SetPresentationSlot":
                    case "ClearPresentationSlot":
                    case "EditPresentationExitPolicy":
                    case "MigratePresentationBindingV1ToV2": ProjectileAuthoringPresentationEditor.Edit(workspace, request, result); break;
                    default: return Block(result, "PROJAUTH_MUTATION_KIND", "Unknown projectile mutation kind.");
                }
            }
            catch (InvalidOperationException ex)
            {
                return Block(result, "PROJAUTH_MUTATION_BLOCKED", ex.Message);
            }
            result.DirtySources = result.Draft.Documents.Select(x => new ProjectileAuthoringDirtySource { RelativePath = NormalizePath(x.SourceRelativePath), Operation = x.Operation }).ToList();
            result.Status = result.Diagnostics.Exists(d => string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase)) ? "Blocked" : "Ready";
            return result;
        }

        private static void CreateDefinition(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, JsonSerializerOptions options, ProjectileAuthoringMutationResult result)
        {
            string id = RequireUniqueStableId(workspace, request.StableId);
            int numeric = AllocateNumericId(workspace, request, result);
            string definitionStableId = request.DefinitionStableId.Length == 0 ? id : request.DefinitionStableId;
            RequireUniqueDefinitionStableId(workspace, definitionStableId);
            var document = new ProjectileRequestAuthoringDocument
            {
                StableId = id,
                Definition = new ProjectileDefinitionConfig
                {
                    DefinitionId = numeric,
                    StableId = definitionStableId,
                    Motion = new ProjectileMotionConfig { Kind = "Linear", SpeedPerFrame = 0.25f, LifetimeFrames = 120 },
                    Collision = new ProjectileCollisionConfig { Radius = 0.15f, MaxAcceptedImpacts = 1, StopOnFirstAcceptedImpact = true },
                    Impact = new ProjectileImpactConfig { PolicyKind = "DirectHit", HitEvent = DefaultHit(id + ".hit") }
                },
                PresentationBinding = new ProjectilePresentationBindingConfig { ProjectileDefinitionId = numeric, ViewNamePrefix = "ProjectileVFX" }
            };
            if (request.CreateRequest)
                document.SpawnProfile = DefaultSpawn(id, document.Definition.StableId, workspace);
            AddUpsert(result, PathFor(id), ProjectileAuthoringHashes.Missing, document, options);
        }

        private static void CreateRequest(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, JsonSerializerOptions options, ProjectileAuthoringMutationResult result)
        {
            string id = RequireUniqueStableId(workspace, request.StableId);
            string definitionId = RequireDefinition(workspace, request.DefinitionStableId).StableId;
            AddUpsert(result, PathFor(id), ProjectileAuthoringHashes.Missing, new ProjectileRequestAuthoringDocument
            {
                StableId = id,
                SpawnProfile = DefaultSpawn(id, definitionId, workspace)
            }, options);
            AddSharedImpact(workspace, definitionId, result);
        }

        private static void DuplicateDefinition(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, JsonSerializerOptions options, ProjectileAuthoringMutationResult result)
        {
            ProjectileDefinitionConfig source = ResolveDefinitionForDuplicate(workspace, request.SourceStableId, out ProjectileAuthoringSourceDocument requestSource);
            string requestId = RequireUniqueStableId(workspace, request.StableId);
            int numeric = AllocateNumericId(workspace, request, result);
            string definitionStableId = request.DefinitionStableId.Length == 0 ? requestId : request.DefinitionStableId;
            RequireUniqueDefinitionStableId(workspace, definitionStableId);
            JsonObject definitionNode = RequireUniqueSubtree(workspace, value => value.Projection?.Definition?.DefinitionId == source.DefinitionId, "definition", "Canonical definition source");
            definitionNode["definitionId"] = numeric;
            definitionNode["stableId"] = definitionStableId;
            JsonObject root = new JsonObject { ["schemaVersion"] = "1", ["stableId"] = requestId, ["definition"] = definitionNode };
            if (requestSource != null)
            {
                JsonObject spawnNode = RequireSubtree(requestSource, "spawnProfile", "Source spawn profile");
                spawnNode["stableId"] = requestId;
                spawnNode["projectileStableId"] = definitionStableId;
                spawnNode["profileId"] = AllocateSpawnProfileId(workspace);
                root["spawnProfile"] = spawnNode;
            }
            else
            {
                root["spawnProfile"] = JsonSerializer.SerializeToNode(DefaultSpawn(requestId, definitionStableId, workspace), options);
            }
            if (request.CopyPresentation && workspace.ReferenceIndex.BindingsByDefinitionId.ContainsKey(source.DefinitionId))
            {
                JsonObject bindingNode = RequireUniqueSubtree(workspace, value => value.Projection?.PresentationBinding?.ProjectileDefinitionId == source.DefinitionId, "presentationBinding", "Canonical presentation binding source");
                bindingNode["projectileDefinitionId"] = numeric;
                root["presentationBinding"] = bindingNode;
            }
            AddJsonUpsert(result, PathFor(requestId), ProjectileAuthoringHashes.Missing, root);
        }

        private static void DuplicateRequest(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, JsonSerializerOptions options, ProjectileAuthoringMutationResult result)
        {
            ProjectileAuthoringSourceDocument source = RequireRequestSource(workspace, request.SourceStableId);
            string id = RequireUniqueStableId(workspace, request.StableId);
            if (source.Projection.SpawnProfile == null) throw new InvalidOperationException("Source request has no spawn profile.");
            JsonObject spawn = RequireSubtree(source, "spawnProfile", "Source spawn profile");
            spawn["stableId"] = id;
            spawn["profileId"] = AllocateSpawnProfileId(workspace);
            var root = new JsonObject { ["schemaVersion"] = "1", ["stableId"] = id, ["spawnProfile"] = spawn };
            AddJsonUpsert(result, PathFor(id), ProjectileAuthoringHashes.Missing, root);
            AddSharedImpact(workspace, source.Projection.SpawnProfile.ProjectileStableId, result);
        }

        private static void RenameRequest(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, JsonSerializerOptions options, ProjectileAuthoringMutationResult result)
        {
            ProjectileAuthoringSourceDocument source = RequireRequestSource(workspace, request.SourceStableId);
            string id = RequireUniqueStableId(workspace, request.StableId);
            BuildConsumerImpact(workspace, request.SourceStableId, request, result);
            if (request.PlanOnly) return;
            RequireAuthorizedImpact(request, result);
            AddDelete(result, source);
            JsonNode renamed = JsonNode.Parse(source.OriginalUtf8Bytes);
            renamed["stableId"] = id;
            if (renamed["spawnProfile"] is JsonObject spawn) spawn["stableId"] = id;
            AddJsonUpsert(result, PathFor(id), ProjectileAuthoringHashes.Missing, renamed);
            AddAuthorizedConsumerUpdates(workspace, request.SourceStableId, id, request, result);
            result.Impact.Summary = "Request identity and authorized consumers move to " + id;
        }

        private static void RenameDefinition(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, JsonSerializerOptions options, ProjectileAuthoringMutationResult result)
        {
            ProjectileDefinitionConfig definition = RequireDefinition(workspace, request.SourceStableId);
            string id = request.DefinitionStableId;
            if (string.IsNullOrWhiteSpace(id) || workspace.ReferenceIndex.DefinitionsByStableId.ContainsKey(id)) throw new InvalidOperationException("Definition stable ID must be non-empty and unique.");
            BuildConsumerImpact(workspace, definition.StableId, request, result);
            if (request.PlanOnly) return;
            RequireAuthorizedImpact(request, result);
            for (int i = 0; i < workspace.SourceDocuments.Count; i++)
            {
                ProjectileAuthoringSourceDocument source = workspace.SourceDocuments[i];
                bool changed = false;
                JsonNode clone = JsonNode.Parse(source.OriginalUtf8Bytes);
                if (source.Projection.Definition != null && string.Equals(source.Projection.Definition.StableId, definition.StableId, StringComparison.Ordinal)) { clone["definition"]["stableId"] = id; changed = true; }
                if (source.Projection.SpawnProfile != null && string.Equals(source.Projection.SpawnProfile.ProjectileStableId, definition.StableId, StringComparison.Ordinal)) { clone["spawnProfile"]["projectileStableId"] = id; changed = true; }
                if (changed) AddJsonUpsert(result, source.RelativePath, source.Sha256, clone);
            }
            AddAuthorizedConsumerUpdates(workspace, definition.StableId, id, request, result);
            AddSharedImpact(workspace, definition.StableId, result);
        }

        private static void DeleteRequest(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result)
        {
            ProjectileAuthoringSourceDocument source = RequireRequestSource(workspace, request.SourceStableId);
            BuildConsumerImpact(workspace, request.SourceStableId, request, result);
            if (result.Impact.ConsumerPaths.Count > 0 && !result.Impact.BlockingReasons.Contains("delete-consumer-update-unsupported", StringComparer.Ordinal))
            {
                result.Impact.BlockingReasons.Add("delete-consumer-update-unsupported");
                result.Impact.Blocking = true;
            }
            if (request.PlanOnly) return;
            RequireAuthorizedImpact(request, result);
            if (source.Projection.Definition != null || source.Projection.PresentationBinding != null)
            {
                JsonNode retained = JsonNode.Parse(source.OriginalUtf8Bytes);
                retained.AsObject().Remove("spawnProfile");
                AddJsonUpsert(result, source.RelativePath, source.Sha256, retained);
            }
            else AddDelete(result, source);
            if (source.Projection.SpawnProfile != null) AddSharedImpact(workspace, source.Projection.SpawnProfile.ProjectileStableId, result);
        }

        private static void DeleteDefinition(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result)
        {
            ProjectileDefinitionConfig definition = RequireDefinition(workspace, request.SourceStableId);
            BuildConsumerImpact(workspace, definition.StableId, request, result);
            if (result.Impact.ConsumerPaths.Count > 0 && !result.Impact.BlockingReasons.Contains("delete-consumer-update-unsupported", StringComparer.Ordinal))
            {
                result.Impact.BlockingReasons.Add("delete-consumer-update-unsupported");
                result.Impact.Blocking = true;
            }
            if (request.PlanOnly) return;
            RequireAuthorizedImpact(request, result);
            AddSharedImpact(workspace, definition.StableId, result);
            if (result.Impact.RequestStableIds.Count > 0) throw new InvalidOperationException("DeleteDefinition is blocked while requests reference the definition.");
            ProjectileAuthoringSourceDocument source = workspace.SourceDocuments.FirstOrDefault(value => value.Projection.Definition != null && value.Projection.Definition.DefinitionId == definition.DefinitionId);
            if (source == null) throw new InvalidOperationException("Canonical definition source was not found.");
            if (!request.ConfirmDestructive) throw new InvalidOperationException("DeleteDefinition requires explicit destructive confirmation.");
            if (source.Projection.SpawnProfile != null)
            {
                JsonNode retained = JsonNode.Parse(source.OriginalUtf8Bytes);
                retained.AsObject().Remove("definition");
                retained.AsObject().Remove("presentationBinding");
                AddJsonUpsert(result, source.RelativePath, source.Sha256, retained);
            }
            else AddDelete(result, source);
        }

        private static void AdoptGenerated(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, JsonSerializerOptions options, ProjectileAuthoringMutationResult result)
        {
            if (workspace.Diagnostics.Any(value => string.Equals(value.Code, "PROJAUTH_GENERATED_CATALOG_INVALID", StringComparison.Ordinal)))
                throw new InvalidOperationException("Generated-only adoption is blocked because the generated projectile inventory is malformed.");
            ProjectileAuthoringGeneratedOnlyEntry generated = workspace.GeneratedOnly.FirstOrDefault(x => string.Equals(x.StableId, request.GeneratedOnlyStableId, StringComparison.Ordinal));
            ProjectileDefinitionConfig definition = generated?.Definition ?? request.GeneratedDefinition;
            ProjectilePresentationBindingConfig binding = generated?.PresentationBinding ?? request.GeneratedPresentationBinding;
            ProjectileSpawnProfileConfig generatedSpawn = generated?.SpawnProfile ?? request.GeneratedSpawnProfile;
            if (!string.IsNullOrWhiteSpace(request.GeneratedOnlyStableId) && generated == null)
                throw new InvalidOperationException("Generated-only stable ID was not found in the workspace inventory.");
            if (definition == null || definition.DefinitionId <= 0 || string.IsNullOrWhiteSpace(definition.StableId)
                || definition.Motion == null || definition.Collision == null || definition.Impact == null || definition.Impact.HitEvent == null
                || binding == null || binding.ProjectileDefinitionId != definition.DefinitionId)
                throw new InvalidOperationException("Adopt requires complete generated definition v3 and presentation binding v1 records.");
            if (workspace.ReferenceIndex.DefinitionsByStableId.ContainsKey(definition.StableId) || workspace.ReferenceIndex.DefinitionsByNumericId.ContainsKey(definition.DefinitionId))
                throw new InvalidOperationException("Generated identity already exists in source authority.");
            string sourceId = string.IsNullOrWhiteSpace(request.StableId) ? definition.StableId : RequireUniqueStableId(workspace, request.StableId);
            ProjectileSpawnProfileConfig spawn = generatedSpawn;
            if (spawn != null && (string.IsNullOrWhiteSpace(spawn.StableId) || !string.Equals(spawn.ProjectileStableId, definition.StableId, StringComparison.Ordinal)))
                throw new InvalidOperationException("Generated spawn profile v1 is incomplete or references another definition.");
            AddUpsert(result, PathFor(sourceId), ProjectileAuthoringHashes.Missing, new ProjectileRequestAuthoringDocument
            {
                StableId = sourceId,
                Definition = Clone(definition, options),
                SpawnProfile = spawn == null ? null : Clone(spawn, options),
                PresentationBinding = Clone(binding, options),
            }, options);
            result.Impact.StableId = definition.StableId;
            result.Impact.Summary = "Generated-only v3/1/1 record is ready to adopt as explicit source.";
        }

        private static int AllocateNumericId(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result)
        {
            var used = new HashSet<int>(workspace.AuthoritativeDefinitionIds);
            used.UnionWith(request.ReservedDefinitionIds ?? new List<int>());
            used.UnionWith(request.DependencyDefinitionIds ?? new List<int>());
            if (request.DefinitionId > 0)
            {
                if (used.Contains(request.DefinitionId)) throw new InvalidOperationException("Requested definition ID collides with package, dependency, or reserved identity.");
                return request.DefinitionId;
            }
            int id = 1;
            while (used.Contains(id)) id++;
            return id;
        }

        private static ProjectileDefinitionConfig RequireDefinition(ProjectileAuthoringWorkspace workspace, string id)
        {
            if (!workspace.ReferenceIndex.DefinitionsByStableId.TryGetValue(id ?? string.Empty, out ProjectileDefinitionConfig value))
                throw new InvalidOperationException("Projectile definition was not found: " + id);
            return value;
        }

        private static ProjectileDefinitionConfig ResolveDefinitionForDuplicate(ProjectileAuthoringWorkspace workspace, string id, out ProjectileAuthoringSourceDocument requestSource)
        {
            requestSource = null;
            if (workspace.ReferenceIndex.DefinitionsByStableId.TryGetValue(id ?? string.Empty, out ProjectileDefinitionConfig definition))
            {
                ProjectileAuthoringSourceDocument[] candidates = workspace.SourceDocuments.Where(value => value.Projection?.SpawnProfile != null && string.Equals(value.SourceStableId, id, StringComparison.Ordinal)).ToArray();
                if (candidates.Length > 1) throw new InvalidOperationException("Source request could not be located unambiguously.");
                requestSource = candidates.SingleOrDefault();
                return definition;
            }
            requestSource = RequireRequestSource(workspace, id);
            return RequireDefinition(workspace, requestSource.Projection.SpawnProfile.ProjectileStableId);
        }

        private static ProjectileAuthoringSourceDocument RequireRequestSource(ProjectileAuthoringWorkspace workspace, string id)
        {
            if (!workspace.ReferenceIndex.RequestsByStableId.TryGetValue(id ?? string.Empty, out ProjectileAuthoringSourceDocument value))
                throw new InvalidOperationException("Projectile request was not found: " + id);
            return value;
        }

        private static JsonObject RequireUniqueSubtree(ProjectileAuthoringWorkspace workspace, Func<ProjectileAuthoringSourceDocument, bool> predicate, string property, string description)
        {
            ProjectileAuthoringSourceDocument[] matches = workspace.SourceDocuments.Where(predicate).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException(description + " could not be located unambiguously.");
            return RequireSubtree(matches[0], property, description);
        }

        private static JsonObject RequireSubtree(ProjectileAuthoringSourceDocument source, string property, string description)
        {
            JsonNode root = JsonNode.Parse(source.OriginalUtf8Bytes);
            if (root is not JsonObject obj || obj[property] is not JsonObject subtree)
                throw new InvalidOperationException(description + " JSON subtree was not found.");
            return (JsonObject)subtree.DeepClone();
        }

        private static string RequireUniqueStableId(ProjectileAuthoringWorkspace workspace, string id)
        {
            id = (id ?? string.Empty).Trim();
            if (id.Length == 0) throw new InvalidOperationException("Stable ID is required.");
            if (workspace.ReferenceIndex.SourceDocumentsByStableId.ContainsKey(id) || workspace.ReferenceIndex.DefinitionsByStableId.ContainsKey(id))
                throw new InvalidOperationException("Stable ID already exists: " + id);
            return id;
        }

        private static void RequireUniqueDefinitionStableId(ProjectileAuthoringWorkspace workspace, string id)
        {
            if (string.IsNullOrWhiteSpace(id) || workspace.ReferenceIndex.DefinitionsByStableId.ContainsKey(id))
                throw new InvalidOperationException("Definition stable ID must be non-empty and unique: " + id);
        }

        private static void AddSharedImpact(ProjectileAuthoringWorkspace workspace, string definitionId, ProjectileAuthoringMutationResult result)
        {
            result.Impact.StableId = definitionId;
            if (workspace.ReferenceIndex.RequestIdsByDefinitionStableId.TryGetValue(definitionId, out IReadOnlyList<string> requests)) result.Impact.RequestStableIds.AddRange(requests);
            result.Impact.Summary = result.Impact.RequestStableIds.Count + " request(s) share this definition.";
        }

        private static void AddUpsert(ProjectileAuthoringMutationResult result, string path, string expected, ProjectileRequestAuthoringDocument document, JsonSerializerOptions options)
        {
            var writeOptions = new JsonSerializerOptions(options) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };
            using JsonDocument json = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(document, writeOptions));
            result.Draft.Documents.Add(new ProjectileAuthoringDraftDocument { Operation = "Upsert", SourceRelativePath = path, ExpectedSha256 = expected, Json = json.RootElement.Clone() });
        }

        private static void AddJsonUpsert(ProjectileAuthoringMutationResult result, string path, string expected, JsonNode node)
        {
            using JsonDocument json = JsonDocument.Parse(node.ToJsonString());
            result.Draft.Documents.Add(new ProjectileAuthoringDraftDocument { Operation = "Upsert", SourceRelativePath = path, ExpectedSha256 = expected, Json = json.RootElement.Clone() });
        }

        private static void BuildConsumerImpact(ProjectileAuthoringWorkspace workspace, string stableId, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result)
        {
            result.Impact.StableId = stableId ?? string.Empty;
            string[] files = Directory.GetFiles(Path.Combine(workspace.PackageRoot, "config"), "*.json", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = NormalizePath(Path.GetRelativePath(workspace.PackageRoot, files[i]));
                if (relative.StartsWith("config/projectiles/", StringComparison.Ordinal)) continue;
                try
                {
                    using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(files[i]));
                    if (ContainsExactString(json.RootElement, stableId)) result.Impact.ConsumerPaths.Add(relative);
                }
                catch (JsonException) { }
            }
            result.Impact.ConsumerPaths.Sort(StringComparer.Ordinal);
            result.Impact.ConfirmationToken = ProjectileAuthoringHashes.ComputeSha256(System.Text.Encoding.UTF8.GetBytes((stableId ?? string.Empty) + "\n" + string.Join("\n", result.Impact.ConsumerPaths)));
            List<string> authorized = request.AuthorizedConsumerPaths ?? new List<string>();
            for (int i = 0; i < result.Impact.ConsumerPaths.Count; i++)
                if (!authorized.Contains(result.Impact.ConsumerPaths[i], StringComparer.Ordinal))
                {
                    result.Impact.AuthorizationRequiredPaths.Add(result.Impact.ConsumerPaths[i]);
                    result.Impact.BlockingReasons.Add("unauthorized-consumer:" + result.Impact.ConsumerPaths[i]);
                }
            if (result.Impact.ConsumerPaths.Count > 0 && (!request.Confirmed || !string.Equals(request.ConfirmationToken, result.Impact.ConfirmationToken, StringComparison.Ordinal)))
                result.Impact.BlockingReasons.Add("confirmation-required");
            result.Impact.Blocking = result.Impact.BlockingReasons.Count > 0;
        }

        private static void RequireAuthorizedImpact(ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result)
        {
            if (result.Impact.Blocking) throw new InvalidOperationException("Consumer impact is blocking: " + string.Join(", ", result.Impact.BlockingReasons));
        }

        private static void AddAuthorizedConsumerUpdates(ProjectileAuthoringWorkspace workspace, string oldId, string newId, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result)
        {
            for (int i = 0; i < result.Impact.ConsumerPaths.Count; i++)
            {
                string relative = result.Impact.ConsumerPaths[i];
                string full = Path.Combine(workspace.PackageRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                byte[] bytes = File.ReadAllBytes(full);
                JsonNode root = JsonNode.Parse(bytes);
                ReplaceExactStrings(root, oldId, newId);
                using JsonDocument json = JsonDocument.Parse(root.ToJsonString());
                result.Draft.Documents.Add(new ProjectileAuthoringDraftDocument { Operation = "UpdateConsumer", SourceRelativePath = relative, ExpectedSha256 = ProjectileAuthoringHashes.ComputeSha256(bytes), Json = json.RootElement.Clone() });
            }
        }

        private static bool ContainsExactString(JsonElement value, string target)
        {
            if (value.ValueKind == JsonValueKind.String) return string.Equals(value.GetString(), target, StringComparison.Ordinal);
            if (value.ValueKind == JsonValueKind.Array) { foreach (JsonElement item in value.EnumerateArray()) if (ContainsExactString(item, target)) return true; }
            if (value.ValueKind == JsonValueKind.Object) { foreach (JsonProperty property in value.EnumerateObject()) if (ContainsExactString(property.Value, target)) return true; }
            return false;
        }

        private static void ReplaceExactStrings(JsonNode node, string oldId, string newId)
        {
            if (node is JsonObject obj)
            {
                foreach (string key in obj.Select(x => x.Key).ToArray())
                {
                    JsonNode child = obj[key];
                    if (child is JsonValue value && value.TryGetValue(out string text) && string.Equals(text, oldId, StringComparison.Ordinal)) obj[key] = newId;
                    else if (child != null) ReplaceExactStrings(child, oldId, newId);
                }
            }
            else if (node is JsonArray array)
                for (int i = 0; i < array.Count; i++) { JsonNode child = array[i]; if (child is JsonValue value && value.TryGetValue(out string text) && string.Equals(text, oldId, StringComparison.Ordinal)) array[i] = newId; else if (child != null) ReplaceExactStrings(child, oldId, newId); }
        }

        private static string NormalizePath(string value) => (value ?? string.Empty).Replace('\\', '/').TrimStart('/');

        private static void AddDelete(ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source)
        {
            result.Draft.Documents.Add(new ProjectileAuthoringDraftDocument { Operation = "Delete", SourceRelativePath = source.RelativePath, ExpectedSha256 = source.Sha256 });
        }

        private static ProjectileAuthoringMutationResult Block(ProjectileAuthoringMutationResult result, string code, string message)
        {
            result.Status = "Blocked";
            result.Diagnostics.Add(new ProjectileAuthoringDiagnostic { Code = code, Severity = "Error", Message = message });
            return result;
        }

        private static T Clone<T>(T value, JsonSerializerOptions options) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, options), options);
        private static string PathFor(string id) => "config/projectiles/" + id + ".json";
        private static int AllocateSpawnProfileId(ProjectileAuthoringWorkspace workspace)
        {
            var used = new HashSet<int>();
            if (workspace?.AbilityDocument?.ProjectileSpawnProfiles != null)
            {
                foreach (var p in workspace.AbilityDocument.ProjectileSpawnProfiles)
                {
                    if (p != null && p.ProfileId > 0) used.Add(p.ProfileId);
                }
            }
            if (workspace?.SourceDocuments != null)
            {
                foreach (var doc in workspace.SourceDocuments)
                {
                    if (doc?.Projection?.SpawnProfile != null && doc.Projection.SpawnProfile.ProfileId > 0)
                        used.Add(doc.Projection.SpawnProfile.ProfileId);
                }
            }
            int id = 1;
            while (used.Contains(id)) id++;
            return id;
        }

        private static ProjectileSpawnProfileConfig DefaultSpawn(string requestId, string definitionId, ProjectileAuthoringWorkspace workspace = null) => new ProjectileSpawnProfileConfig
        {
            ProfileId = workspace != null ? AllocateSpawnProfileId(workspace) : 0,
            StableId = requestId,
            ProjectileStableId = definitionId,
            Origin = new ProjectileSpawnOriginConfig { Kind = "Socket", SocketLabel = "root" },
            Aim = new ProjectileAimConfig { Kind = "CasterForwardRange", FallbackKind = "CasterForwardRange", MaxRange = 20f }
        };
        private static CombatHitEventConfig DefaultHit(string eventId) => new CombatHitEventConfig { StableEventLabel = eventId ?? string.Empty, TargetTypes = new List<string> { "Enemy" }, MaxAcceptedHits = 1, Damage = new CombatHitDamageConfig { DamageTypeLabel = "Default", BaseDamage = 1 } };
    }

    /// <summary>Replays pending mutations in order and returns one draft guarded against the original workspace.</summary>
    /// <remarks>LowFreqAlloc: uses an isolated temporary package so every command observes prior command semantics.</remarks>
    public static class ProjectileAuthoringMutationSequencePlanner
    {
        public static ProjectileAuthoringMutationResult Plan(
            ProjectileAuthoringWorkspace baseWorkspace,
            ProjectileAuthoringMutationSequenceRequest sequence,
            JsonSerializerOptions options)
        {
            if (baseWorkspace == null) throw new ArgumentNullException(nameof(baseWorkspace));
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            options ??= new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            if (!string.Equals(sequence.SchemaVersion, "1", StringComparison.Ordinal))
                return Block("PROJAUTH_SEQUENCE_SCHEMA", "Only mutation sequence schemaVersion 1 is supported.");
            if (sequence.Commands == null || sequence.Commands.Count == 0)
                return Block("PROJAUTH_SEQUENCE_EMPTY", "Mutation sequence must contain at least one command.");

            string stagedRoot = Path.Combine(Path.GetTempPath(), "MxFramework.ProjectileAuthoring.Sequence", Guid.NewGuid().ToString("N"));
            var touched = new HashSet<string>(StringComparer.Ordinal);
            ProjectileAuthoringMutationResult latest = null;
            try
            {
                CopyDirectory(baseWorkspace.PackageRoot, stagedRoot);
                for (int i = 0; i < sequence.Commands.Count; i++)
                {
                    using ProjectileAuthoringWorkspace current = ProjectileAuthoringWorkspace.Load(
                        stagedRoot,
                        baseWorkspace.GeneratedRoot,
                        baseWorkspace.VfxDefinitionCatalogPath,
                        options);
                    latest = ProjectileAuthoringMutationPlanner.Plan(current, sequence.Commands[i], options);
                    if (!string.Equals(latest.Status, "Ready", StringComparison.Ordinal))
                        return latest;
                    for (int j = 0; j < latest.Draft.Documents.Count; j++)
                    {
                        ProjectileAuthoringDraftDocument proposal = latest.Draft.Documents[j];
                        string relative = Normalize(proposal.SourceRelativePath);
                        touched.Add(relative);
                        string target = SafeCombine(stagedRoot, relative);
                        if (string.Equals(proposal.Operation, "Delete", StringComparison.OrdinalIgnoreCase))
                        {
                            if (File.Exists(target)) File.Delete(target);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? stagedRoot);
                            File.WriteAllText(target, proposal.Json.GetRawText(), new UTF8Encoding(false));
                        }
                    }
                }

                latest ??= new ProjectileAuthoringMutationResult { Status = "Ready" };
                latest.Draft = BuildCompositeDraft(baseWorkspace.PackageRoot, stagedRoot, touched);
                latest.DirtySources = latest.Draft.Documents.Select(document => new ProjectileAuthoringDirtySource
                {
                    RelativePath = document.SourceRelativePath,
                    Operation = document.Operation,
                }).ToList();
                using (ProjectileAuthoringWorkspace projected = ProjectileAuthoringWorkspace.Load(
                    stagedRoot,
                    baseWorkspace.GeneratedRoot,
                    baseWorkspace.VfxDefinitionCatalogPath,
                    options))
                    latest.ProjectedWorkspace = ProjectileAuthoringWorkspaceProjectionBuilder.Build(projected, projected.Compile());
                return latest;
            }
            finally
            {
                try { if (Directory.Exists(stagedRoot)) Directory.Delete(stagedRoot, true); }
                catch { }
            }
        }

        private static ProjectileAuthoringDraftBundle BuildCompositeDraft(string baseRoot, string stagedRoot, IEnumerable<string> touched)
        {
            var draft = new ProjectileAuthoringDraftBundle();
            foreach (string relative in touched.OrderBy(value => value, StringComparer.Ordinal))
            {
                string original = SafeCombine(baseRoot, relative);
                string final = SafeCombine(stagedRoot, relative);
                bool originalExists = File.Exists(original);
                bool finalExists = File.Exists(final);
                if (!originalExists && !finalExists)
                    continue;
                if (originalExists && finalExists && File.ReadAllBytes(original).SequenceEqual(File.ReadAllBytes(final)))
                    continue;
                var document = new ProjectileAuthoringDraftDocument
                {
                    SourceRelativePath = relative,
                    ExpectedSha256 = originalExists ? ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(original)) : ProjectileAuthoringHashes.Missing,
                    Operation = finalExists ? (relative.StartsWith("config/projectiles/", StringComparison.Ordinal) ? "Upsert" : "UpdateConsumer") : "Delete",
                };
                if (finalExists)
                {
                    using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(final));
                    document.Json = json.RootElement.Clone();
                }
                draft.Documents.Add(document);
            }
            return draft;
        }

        private static ProjectileAuthoringMutationResult Block(string code, string message)
        {
            var result = new ProjectileAuthoringMutationResult();
            result.Diagnostics.Add(new ProjectileAuthoringDiagnostic { Code = code, Severity = "Error", Message = message });
            return result;
        }

        private static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/').TrimStart('/');

        private static string SafeCombine(string root, string relative)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Mutation path escapes package root.");
            return full;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
