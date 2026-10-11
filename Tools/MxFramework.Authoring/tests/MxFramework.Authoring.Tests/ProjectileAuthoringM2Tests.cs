using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class ProjectileAuthoringM2Tests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static void RunAll()
    {
        M2C01_WorkspaceContractInventory();
        M2C02_C05_CreateAndDuplicateSemantics();
        M2C06_C08_RenameDeleteGuards();
        M2C09_C10_AdoptAndAllocatorFailClosed();
        M2C11_MutationsArePure();
        M2A_GeneratedInventoryOpaqueAdoptAndAutomaticAuthority();
        M2A_ConsumerImpactMixedDeleteAndDirtyContract();
        M2C21_CreateRenameDeleteRollback();
        M2A_AuthorizedConsumerMutationAppliesAtomically();
        M2C02_C10_OrderedSequenceProducesCompositeBaseDraft();
        M2R4_SequenceReturnsStagedWorkspaceProjectionWithoutDiskWrites();
        M2R4_DuplicatePreservesUnknownSubtrees();
        M2R4_MalformedGeneratedInventoryFailsClosed();
    }

    private static void M2C02_C10_OrderedSequenceProducesCompositeBaseDraft()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        var sequence = new ProjectileAuthoringMutationSequenceRequest
        {
            Commands = new List<ProjectileAuthoringMutationRequest>
            {
                new ProjectileAuthoringMutationRequest { Kind = "CreateDefinition", StableId = "sequence.alpha", DefinitionStableId = "sequence.definition" },
                new ProjectileAuthoringMutationRequest { Kind = "RenameRequest", SourceStableId = "sequence.alpha", StableId = "sequence.beta" },
                new ProjectileAuthoringMutationRequest { Kind = "DuplicateRequest", SourceStableId = "sequence.beta", StableId = "sequence.gamma" },
                new ProjectileAuthoringMutationRequest { Kind = "CreateDefinition", StableId = "sequence.delta", DefinitionStableId = "sequence.delta.definition" },
            }
        };
        ProjectileAuthoringMutationResult result = ProjectileAuthoringMutationSequencePlanner.Plan(workspace, sequence, Options);
        Require(result.Status == "Ready", "M2 sequence replay must allow later commands to address earlier results.");
        Require(result.Draft.Documents.Count == 3, "M2 composite draft must collapse transient alpha into final beta/gamma/delta documents.");
        Require(result.Draft.Documents.All(x => x.ExpectedSha256 == ProjectileAuthoringHashes.Missing), "M2 composite creates must be guarded against the original workspace.");
        Require(result.Draft.Documents.Select(x => x.SourceRelativePath).SequenceEqual(new[] { "config/projectiles/sequence.beta.json", "config/projectiles/sequence.delta.json", "config/projectiles/sequence.gamma.json" }), "M2 composite draft ordering/path set is unstable.");
        int betaDefinitionId = result.Draft.Documents.Single(x => x.SourceRelativePath.EndsWith("sequence.beta.json", StringComparison.Ordinal)).Json.GetProperty("definition").GetProperty("definitionId").GetInt32();
        int deltaDefinitionId = result.Draft.Documents.Single(x => x.SourceRelativePath.EndsWith("sequence.delta.json", StringComparison.Ordinal)).Json.GetProperty("definition").GetProperty("definitionId").GetInt32();
        Require(betaDefinitionId != deltaDefinitionId, "M2 allocator must observe IDs allocated by earlier commands in the same sequence.");

        ProjectileAuthoringMutationResult reversed = ProjectileAuthoringMutationSequencePlanner.Plan(workspace, new ProjectileAuthoringMutationSequenceRequest
        {
            Commands = new List<ProjectileAuthoringMutationRequest>
            {
                new ProjectileAuthoringMutationRequest { Kind = "RenameRequest", SourceStableId = "sequence.alpha", StableId = "sequence.beta" },
                new ProjectileAuthoringMutationRequest { Kind = "CreateDefinition", StableId = "sequence.alpha", DefinitionStableId = "sequence.definition" },
            }
        }, Options);
        Require(reversed.Status == "Blocked", "M2 sequence must preserve order semantics rather than concatenating independent drafts.");

        ProjectileAuthoringMutationResult definitionChain = ProjectileAuthoringMutationSequencePlanner.Plan(workspace, new ProjectileAuthoringMutationSequenceRequest
        {
            Commands = new List<ProjectileAuthoringMutationRequest>
            {
                new ProjectileAuthoringMutationRequest { Kind = "RenameDefinition", SourceStableId = "character_test.meteor_barrage.projectile", DefinitionStableId = "sequence.meteor.definition" },
                new ProjectileAuthoringMutationRequest { Kind = "CreateRequestForExistingDefinition", StableId = "sequence.meteor.request", DefinitionStableId = "sequence.meteor.definition" },
            }
        }, Options);
        Require(definitionChain.Status == "Ready" && definitionChain.Draft.Documents.Count == 8, "M2 definition rename followed by request create must emit seven rebased updates plus one create.");
        Require(definitionChain.Draft.Documents.Count(x => x.ExpectedSha256 != ProjectileAuthoringHashes.Missing) == 7, "M2 definition rename guards must remain relative to all seven original sources.");
    }

    private static void M2R4_SequenceReturnsStagedWorkspaceProjectionWithoutDiskWrites()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-projauth-m2-r4-projection-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(SamplePath(), root);
            Dictionary<string, string> before = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(root, path), path => ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(path)));
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            ProjectileAuthoringMutationResult result = ProjectileAuthoringMutationSequencePlanner.Plan(workspace, new ProjectileAuthoringMutationSequenceRequest
            {
                Commands = new List<ProjectileAuthoringMutationRequest>
                {
                    new ProjectileAuthoringMutationRequest { Kind = "CreateDefinition", StableId = "r4.projectile", DefinitionStableId = "r4.definition" },
                    new ProjectileAuthoringMutationRequest { Kind = "DuplicateRequest", SourceStableId = "r4.projectile", StableId = "r4.request" },
                }
            }, Options);
            Require(result.Status == "Ready" && result.ProjectedWorkspace != null, "M2-R4 mutation sequence must return its staged workspace projection.");
            Require(result.ProjectedWorkspace.Definitions.Any(value => value.StableId == "r4.definition") && result.ProjectedWorkspace.Requests.Any(value => value.StableId == "r4.request"), "M2-R4 projected workspace does not represent the composite draft.");
            Require(result.ProjectedWorkspace.Sources.All(value => !Path.IsPathRooted(value.RelativePath))
                && result.ProjectedWorkspace.Requests.All(value => !Path.IsPathRooted(value.SourcePath))
                && result.ProjectedWorkspace.Bindings.All(value => !Path.IsPathRooted(value.SourcePath))
                && result.ProjectedWorkspace.Definitions.SelectMany(value => value.SourcePaths.Concat(value.PresentationSourcePaths)).All(value => !Path.IsPathRooted(value)), "M2-R4 projected workspace leaked an absolute path.");
            Require(result.ProjectedWorkspace.Definitions.All(value => value.SourcePaths.Count > 0), "M2-R4 definitions must expose exact source ownership.");
            Dictionary<string, string> after = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(root, path), path => ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(path)));
            Require(before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out string hash) && hash == pair.Value), "M2-R4 projection planning mutated the real package.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void M2R4_DuplicatePreservesUnknownSubtrees()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-projauth-m2-r4-unknown-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(SamplePath(), root);
            string path = Path.Combine(root, "config", "projectiles", "character_test.fireball_burst.projectile.json");
            JsonNode node = JsonNode.Parse(File.ReadAllBytes(path))!;
            node["definition"]!["r4UnknownDefinition"] = new JsonObject { ["keep"] = 11 };
            node["spawnProfile"]!["r4UnknownSpawn"] = new JsonArray(12, 13);
            node["presentationBinding"]!["r4UnknownPresentation"] = "keep-me";
            File.WriteAllText(path, node.ToJsonString(Options));
            string meteorDefinitionPath = Path.Combine(root, "config", "projectiles", "character_test.meteor_barrage.projectile.json");
            JsonNode meteorDefinition = JsonNode.Parse(File.ReadAllBytes(meteorDefinitionPath))!;
            meteorDefinition["definition"]!["r4CanonicalUnknown"] = 21;
            meteorDefinition["presentationBinding"]!["r4CanonicalPresentationUnknown"] = 22;
            File.WriteAllText(meteorDefinitionPath, meteorDefinition.ToJsonString(Options));
            string meteorRequestPath = Path.Combine(root, "config", "projectiles", "character_test.meteor_barrage.projectile.0.json");
            JsonNode meteorRequest = JsonNode.Parse(File.ReadAllBytes(meteorRequestPath))!;
            meteorRequest["spawnProfile"]!["r4AliasUnknown"] = 23;
            File.WriteAllText(meteorRequestPath, meteorRequest.ToJsonString(Options));
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            ProjectileAuthoringMutationResult definition = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest
            {
                Kind = "DuplicateDefinition", SourceStableId = "character_test.fireball_burst.projectile", StableId = "r4.copy", DefinitionStableId = "r4.copy.definition"
            }, Options);
            Require(definition.Status == "Ready", "M2-R4 lossless definition duplicate was blocked.");
            JsonElement duplicate = definition.Draft.Documents.Single().Json;
            Require(duplicate.GetProperty("definition").GetProperty("r4UnknownDefinition").GetProperty("keep").GetInt32() == 11
                && duplicate.GetProperty("spawnProfile").GetProperty("r4UnknownSpawn")[1].GetInt32() == 13
                && duplicate.GetProperty("presentationBinding").GetProperty("r4UnknownPresentation").GetString() == "keep-me", "M2-R4 duplicate definition lost an unknown subtree.");
            ProjectileAuthoringMutationResult request = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "DuplicateRequest", SourceStableId = "character_test.fireball_burst.projectile", StableId = "r4.request.copy" }, Options);
            Require(request.Status == "Ready" && request.Draft.Documents.Single().Json.GetProperty("spawnProfile").GetProperty("r4UnknownSpawn")[0].GetInt32() == 12, "M2-R4 duplicate request lost an unknown spawn subtree.");
            ProjectileAuthoringMutationResult scattered = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "DuplicateDefinition", SourceStableId = "character_test.meteor_barrage.projectile.0", StableId = "r4.scattered.copy", DefinitionStableId = "r4.scattered.definition" }, Options);
            JsonElement scatteredJson = scattered.Draft.Documents.Single().Json;
            Require(scattered.Status == "Ready" && scatteredJson.GetProperty("definition").GetProperty("r4CanonicalUnknown").GetInt32() == 21
                && scatteredJson.GetProperty("spawnProfile").GetProperty("r4AliasUnknown").GetInt32() == 23
                && scatteredJson.GetProperty("presentationBinding").GetProperty("r4CanonicalPresentationUnknown").GetInt32() == 22, "M2-R4 scattered canonical subtrees were not resolved losslessly through the reference index.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void M2R4_MalformedGeneratedInventoryFailsClosed()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-projauth-m2-r4-malformed-" + Guid.NewGuid().ToString("N"));
        string package = Path.Combine(root, "package"); string generated = Path.Combine(root, "generated");
        try
        {
            CopyDirectory(SamplePath(), package); Directory.CreateDirectory(generated);
            File.WriteAllText(Path.Combine(generated, "projectile_definitions.json"), "{\"schemaVersion\":\"3\"}");
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(package, generated);
            Require(workspace.Diagnostics.Any(value => value.Code == "PROJAUTH_GENERATED_CATALOG_INVALID" && value.Severity == "Error"), "M2-R4 malformed generated inventory was silently treated as empty.");
            ProjectileAuthoringMutationResult adopt = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "AdoptGeneratedOnly", GeneratedOnlyStableId = "anything" }, Options);
            Require(adopt.Status == "Blocked", "M2-R4 malformed generated inventory must not be adoptable.");
            ProjectileAuthoringMutationResult manualFallback = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest
            {
                Kind = "AdoptGeneratedOnly",
                GeneratedDefinition = new ProjectileDefinitionConfig { DefinitionId = 991111, StableId = "malformed.fallback", Motion = new ProjectileMotionConfig(), Collision = new ProjectileCollisionConfig(), Impact = new ProjectileImpactConfig { HitEvent = new CombatHitEventConfig() } },
                GeneratedPresentationBinding = new ProjectilePresentationBindingConfig { ProjectileDefinitionId = 991111 }
            }, Options);
            Require(manualFallback.Status == "Blocked", "M2-R4 malformed generated inventory must also block manual fallback adoption.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void M2C01_WorkspaceContractInventory()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        Require(workspace.SourceDocuments.Count == 29, "M2-C01 source inventory changed.");
        Require(workspace.ReferenceIndex.DefinitionsByStableId.Count == 23, "M2-C01 definition inventory changed.");
        Require(workspace.ReferenceIndex.RequestsByStableId.Count == 28, "M2-C01 request inventory changed.");
        Require(workspace.SourceDocuments.All(source => !Path.IsPathRooted(source.RelativePath)), "M2-C01 contract path must be relative.");
    }

    private static void M2C02_C05_CreateAndDuplicateSemantics()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        ProjectileAuthoringMutationResult create = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "CreateDefinition", StableId = "test.new.projectile", DefinitionStableId = "test.new.definition", CreateRequest = true, ReservedDefinitionIds = new List<int> { 1, 2, 3 } }, Options);
        Require(create.Status == "Ready" && create.Draft.Documents.Count == 1, "M2-C02 create definition must produce one draft.");
        Require(create.Draft.Documents[0].ExpectedSha256 == ProjectileAuthoringHashes.Missing, "M2-C02 new source guard missing.");
        JsonElement createdJson = create.Draft.Documents[0].Json;
        Require(createdJson.GetProperty("stableId").GetString() == "test.new.projectile" && createdJson.GetProperty("definition").GetProperty("stableId").GetString() == "test.new.definition", "M2-C02 create identities are not represented in the draft.");
        Require(createdJson.GetProperty("definition").GetProperty("motion").GetProperty("kind").GetString() == "Linear" && createdJson.GetProperty("spawnProfile").GetProperty("projectileStableId").GetString() == "test.new.definition", "M2-C02 default motion/request semantics are incomplete.");

        ProjectileAuthoringMutationResult request = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "CreateRequestForExistingDefinition", StableId = "test.request", DefinitionStableId = "character_test.meteor_barrage.projectile" }, Options);
        Require(request.Status == "Ready" && request.Impact.RequestStableIds.Count == 6, "M2-C03 shared request impact is incomplete.");
        Require(!request.Draft.Documents[0].Json.TryGetProperty("definition", out _) && request.Draft.Documents[0].Json.GetProperty("spawnProfile").GetProperty("projectileStableId").GetString() == "character_test.meteor_barrage.projectile", "M2-C03 request creation must reference, not duplicate, the existing definition.");

        ProjectileAuthoringMutationResult duplicateDefinition = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "DuplicateDefinition", SourceStableId = "character_test.fireball_burst.projectile", StableId = "test.copy.request", DefinitionStableId = "test.copy.definition" }, Options);
        Require(duplicateDefinition.Status == "Ready", "M2-C04 duplicate definition was blocked.");
        JsonElement duplicatedDefinition = duplicateDefinition.Draft.Documents[0].Json;
        Require(duplicatedDefinition.GetProperty("definition").GetProperty("stableId").GetString() == "test.copy.definition" && duplicatedDefinition.GetProperty("presentationBinding").GetProperty("projectileDefinitionId").GetInt32() == duplicatedDefinition.GetProperty("definition").GetProperty("definitionId").GetInt32(), "M2-C04 duplicated definition/presentation IDs are inconsistent.");
        Require(duplicatedDefinition.GetProperty("definition").GetProperty("impact").GetProperty("hitEvent").GetProperty("stableEventLabel").GetString() == "character_test.fireball_burst.projectile.impact", "M2-C04 duplicate lost nested impact semantics.");

        ProjectileAuthoringMutationResult duplicateRequest = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "DuplicateRequest", SourceStableId = "character_test.meteor_barrage.projectile.0", StableId = "test.meteor.variant" }, Options);
        Require(duplicateRequest.Status == "Ready" && duplicateRequest.Impact.RequestStableIds.Count == 6, "M2-C05 duplicate request must retain shared definition.");
        Require(!duplicateRequest.Draft.Documents[0].Json.TryGetProperty("definition", out _) && duplicateRequest.Draft.Documents[0].Json.GetProperty("spawnProfile").GetProperty("projectileStableId").GetString() == "character_test.meteor_barrage.projectile", "M2-C05 duplicate request must remain a spawn-only alias.");
    }

    private static void M2C06_C08_RenameDeleteGuards()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        ProjectileAuthoringMutationResult renameRequest = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "RenameRequest", SourceStableId = "character_test.meteor_barrage.projectile.0", StableId = "test.meteor.renamed" }, Options);
        Require(renameRequest.Status == "Ready" && renameRequest.Draft.Documents.Count == 2, "M2-C06 rename request must delete+upsert.");
        Require(renameRequest.Draft.Documents.Any(x => x.Operation == "Delete" && x.ExpectedSha256 != ProjectileAuthoringHashes.Missing) && renameRequest.Draft.Documents.Any(x => x.Operation == "Upsert" && x.ExpectedSha256 == ProjectileAuthoringHashes.Missing && x.Json.GetProperty("spawnProfile").GetProperty("stableId").GetString() == "test.meteor.renamed"), "M2-C06 rename request guards or nested identity are wrong.");

        ProjectileAuthoringMutationResult renameDefinition = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "RenameDefinition", SourceStableId = "character_test.meteor_barrage.projectile", DefinitionStableId = "test.meteor.definition" }, Options);
        Require(renameDefinition.Status == "Ready" && renameDefinition.Draft.Documents.Count == 7, "M2-C07 rename definition must update canonical and all six spawn aliases.");
        Require(renameDefinition.Draft.Documents.All(x => x.ExpectedSha256 != ProjectileAuthoringHashes.Missing) && renameDefinition.Draft.Documents.Count(x => x.Json.TryGetProperty("spawnProfile", out JsonElement spawn) && spawn.GetProperty("projectileStableId").GetString() == "test.meteor.definition") == 6, "M2-C07 all six request references must be rewritten under original hash guards.");

        ProjectileAuthoringMutationResult deleteRequest = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "DeleteRequest", SourceStableId = "character_test.meteor_barrage.projectile.0" }, Options);
        Require(deleteRequest.Status == "Ready" && deleteRequest.Draft.Documents.Count == 1, "M2-C08 delete request must not implicitly delete definition.");
        Require(deleteRequest.Draft.Documents[0].Operation == "Delete" && deleteRequest.Draft.Documents[0].ExpectedSha256 != ProjectileAuthoringHashes.Missing, "M2-C08 spawn-only delete must be hash guarded.");
        ProjectileAuthoringMutationResult deleteDefinition = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "DeleteDefinition", SourceStableId = "character_test.meteor_barrage.projectile", ConfirmDestructive = true }, Options);
        Require(deleteDefinition.Status == "Blocked", "M2-C08 shared definition delete must be blocked.");
    }

    private static void M2C09_C10_AdoptAndAllocatorFailClosed()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        ProjectileAuthoringMutationResult adopt = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "AdoptGeneratedOnly", StableId = "missing" }, Options);
        Require(adopt.Status == "Blocked", "M2-C09 incomplete adopt must fail closed.");
        var generatedDefinition = new ProjectileDefinitionConfig
        {
            DefinitionId = 900001,
            StableId = "generated.only.projectile",
            Motion = new ProjectileMotionConfig { Kind = "Linear", SpeedPerFrame = 1, LifetimeFrames = 30 },
            Collision = new ProjectileCollisionConfig { Radius = 0.1f, MaxAcceptedImpacts = 1 },
            Impact = new ProjectileImpactConfig
            {
                PolicyKind = "DirectHit",
                HitEvent = new CombatHitEventConfig { StableEventLabel = "generated.hit", TargetTypes = new List<string> { "Enemy" }, Damage = new CombatHitDamageConfig { DamageTypeLabel = "Default", BaseDamage = 1 } }
            }
        };
        ProjectileAuthoringMutationResult adopted = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest
        {
            Kind = "AdoptGeneratedOnly",
            GeneratedDefinition = generatedDefinition,
            GeneratedPresentationBinding = new ProjectilePresentationBindingConfig { ProjectileDefinitionId = 900001, ViewNamePrefix = "ProjectileVFX" }
        }, Options);
        Require(adopted.Status == "Ready" && adopted.Draft.Documents.Count == 1, "M2-C09 complete generated v3/1/1 record should be adoptable.");
        int existing = workspace.ReferenceIndex.DefinitionsByNumericId.Keys.First();
        ProjectileAuthoringMutationResult collision = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "CreateDefinition", StableId = "test.collision", DefinitionId = existing }, Options);
        Require(collision.Status == "Blocked", "M2-C10 package numeric collision must be blocked.");
        ProjectileAuthoringMutationResult dependencyCollision = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "CreateDefinition", StableId = "test.dependency.collision", DefinitionId = 990009, DependencyDefinitionIds = new List<int> { 990009 } }, Options);
        Require(dependencyCollision.Status == "Blocked" && dependencyCollision.Diagnostics.Any(x => x.Code == "PROJAUTH_MUTATION_BLOCKED"), "M2-C10 dependency authority collision must fail closed with a diagnostic.");
    }

    private static void M2C11_MutationsArePure()
    {
        string path = SamplePath();
        Dictionary<string, DateTime> before = Directory.GetFiles(Path.Combine(path, "config", "projectiles"), "*.json").ToDictionary(file => file, File.GetLastWriteTimeUtc);
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(path);
        ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "CreateDefinition", StableId = "test.pure" }, Options);
        Require(before.All(pair => File.GetLastWriteTimeUtc(pair.Key) == pair.Value), "M2-C11 planner wrote real source files.");
    }

    private static void M2A_GeneratedInventoryOpaqueAdoptAndAutomaticAuthority()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-projauth-m2-generated-" + Guid.NewGuid().ToString("N"));
        string package = Path.Combine(root, "package");
        string generated = Path.Combine(root, "generated");
        try
        {
            CopyDirectory(SamplePath(), package);
            Directory.CreateDirectory(generated);
            using (ProjectileAuthoringWorkspace source = LoadWorkspace(package))
            {
                ProjectileAuthoringCompileResult compiled = source.Compile();
                var definition = new ProjectileDefinitionConfig
                {
                    DefinitionId = 990001, StableId = "generated.opaque.projectile",
                    Motion = new ProjectileMotionConfig { Kind = "Linear", SpeedPerFrame = 1, LifetimeFrames = 30 },
                    Collision = new ProjectileCollisionConfig { Radius = .1f, MaxAcceptedImpacts = 1 },
                    Impact = new ProjectileImpactConfig { PolicyKind = "DirectHit", HitEvent = new CombatHitEventConfig { StableEventLabel = "generated.opaque.hit", TargetTypes = new List<string> { "Enemy" }, Damage = new CombatHitDamageConfig { DamageTypeLabel = "Default", BaseDamage = 1 } } }
                };
                compiled.Definitions.Definitions.Add(definition);
                compiled.PresentationBindings.Bindings.Add(new ProjectilePresentationBindingConfig { ProjectileDefinitionId = 990001, ViewNamePrefix = "ProjectileVFX" });
                File.WriteAllText(Path.Combine(generated, "projectile_definitions.json"), JsonSerializer.Serialize(compiled.Definitions, Options));
                File.WriteAllText(Path.Combine(generated, "projectile_spawn_profiles.json"), JsonSerializer.Serialize(compiled.SpawnProfiles, Options));
                File.WriteAllText(Path.Combine(generated, "projectile_presentation_bindings.json"), JsonSerializer.Serialize(compiled.PresentationBindings, Options));
            }
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(package, generated);
            Require(workspace.GeneratedOnly.Count == 1 && workspace.GeneratedOnly[0].Complete, "M2-A generated-only inventory must expose a complete opaque entry.");
            ProjectileAuthoringMutationResult adopted = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "AdoptGeneratedOnly", GeneratedOnlyStableId = "generated.opaque.projectile" }, Options);
            Require(adopted.Status == "Ready" && adopted.Draft.Documents.Count == 1, "M2-A opaque generated-only adopt failed.");
            ProjectileAuthoringMutationResult collision = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "CreateDefinition", StableId = "automatic.authority", DefinitionId = 12013 }, Options);
            Require(collision.Status == "Blocked", "M2-A allocator must discover package-wide definition ID authority automatically.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void M2A_ConsumerImpactMixedDeleteAndDirtyContract()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-projauth-m2-impact-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(SamplePath(), root);
            File.WriteAllText(Path.Combine(root, "config", "test_consumer.json"), "{\"request\":\"character_test.fireball.projectile\"}");
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            var previewRequest = new ProjectileAuthoringMutationRequest { Kind = "RenameRequest", SourceStableId = "character_test.fireball.projectile", StableId = "test.fireball.renamed", PlanOnly = true };
            ProjectileAuthoringMutationResult preview = ProjectileAuthoringMutationPlanner.Plan(workspace, previewRequest, Options);
            Require(preview.Status == "Ready" && preview.Draft.Documents.Count == 0 && preview.Impact.Blocking && preview.Impact.AuthorizationRequiredPaths.SequenceEqual(new[] { "config/test_consumer.json" }), "M2-A plan-only consumer preview must be actionable without producing a draft.");
            var confirmed = new ProjectileAuthoringMutationRequest { Kind = "RenameRequest", SourceStableId = "character_test.fireball.projectile", StableId = "test.fireball.renamed", Confirmed = true, ConfirmationToken = preview.Impact.ConfirmationToken, AuthorizedConsumerPaths = new List<string> { "config/test_consumer.json" } };
            ProjectileAuthoringMutationResult rename = ProjectileAuthoringMutationPlanner.Plan(workspace, confirmed, Options);
            Require(rename.Status == "Ready" && rename.DirtySources.Count == 3 && rename.DirtySources.All(x => !Path.IsPathRooted(x.RelativePath)), "M2-A rename dirty source operations are inaccurate.");
            ProjectileAuthoringMutationResult delete = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "DeleteRequest", SourceStableId = "character_test.fireball_burst.projectile" }, Options);
            Require(delete.Status == "Ready" && delete.Draft.Documents[0].Operation == "Upsert" && delete.Draft.Documents[0].Json.GetProperty("definition").ValueKind == JsonValueKind.Object && !delete.Draft.Documents[0].Json.TryGetProperty("spawnProfile", out _), "M2-A mixed source request deletion must retain canonical definition.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void M2C21_CreateRenameDeleteRollback()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-projauth-m2-rollback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string rename = Path.Combine(root, "rename.json");
        string delete = Path.Combine(root, "delete.json");
        string create = Path.Combine(root, "create.json");
        File.WriteAllText(rename, "old-rename"); File.WriteAllText(delete, "old-delete");
        try
        {
            var items = new List<ProjectileAuthoringTransactionItem>
            {
                new ProjectileAuthoringTransactionItem { ReceiptPath = "config/projectiles/create.json", TargetPath = create, ExpectedSha256 = ProjectileAuthoringHashes.Missing, Content = System.Text.Encoding.UTF8.GetBytes("new-create") },
                new ProjectileAuthoringTransactionItem { ReceiptPath = "config/projectiles/rename.json", TargetPath = rename, ExpectedSha256 = ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(rename)), Content = System.Text.Encoding.UTF8.GetBytes("new-rename") },
                new ProjectileAuthoringTransactionItem { ReceiptPath = "config/projectiles/delete.json", TargetPath = delete, ExpectedSha256 = ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(delete)), Delete = true },
            };
            ProjectileAuthoringTransactionReceipt receipt = ProjectileAuthoringTransaction.Execute(items, new FailAfterTwoCommits());
            Require(receipt.Status == "RolledBack" && !File.Exists(create) && File.ReadAllText(rename) == "old-rename" && File.ReadAllText(delete) == "old-delete", "M2-C21 create/rename/delete rollback did not restore all source states.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void M2A_AuthorizedConsumerMutationAppliesAtomically()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-projauth-m2-consumer-apply-" + Guid.NewGuid().ToString("N"));
        string package = Path.Combine(root, "package");
        string generated = Path.Combine(root, "generated");
        string requestPath = Path.Combine(root, "request.json");
        string draftPath = Path.Combine(root, "draft.json");
        string failurePackage = Path.Combine(root, "failure-package");
        string failureGenerated = Path.Combine(root, "failure-generated");
        try
        {
            CopyDirectory(SamplePath(), package);
            string consumer = Path.Combine(package, "config", "test_consumer.json");
            File.WriteAllText(consumer, "{\"unknown\":{\"keep\":17},\"request\":\"character_test.fireball.projectile\"}");
            CopyDirectory(package, failurePackage);
            string token;
            using (ProjectileAuthoringWorkspace workspace = LoadWorkspace(package))
            {
                ProjectileAuthoringMutationResult preview = ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = "RenameRequest", SourceStableId = "character_test.fireball.projectile", StableId = "test.fireball.applied", PlanOnly = true }, Options);
                token = preview.Impact.ConfirmationToken;
            }
            File.WriteAllText(requestPath, JsonSerializer.Serialize(new ProjectileAuthoringMutationSequenceRequest
            {
                Commands = new List<ProjectileAuthoringMutationRequest>
                {
                    new ProjectileAuthoringMutationRequest { Kind = "RenameRequest", SourceStableId = "character_test.fireball.projectile", StableId = "test.fireball.applied", Confirmed = true, ConfirmationToken = token, AuthorizedConsumerPaths = new List<string> { "config/test_consumer.json" } },
                    new ProjectileAuthoringMutationRequest { Kind = "DuplicateRequest", SourceStableId = "test.fireball.applied", StableId = "test.fireball.variant" },
                }
            }, Options));
            int mutate = MxFramework.Authoring.Cli.Program.Main(new[] { "projectile", "mutate", "--package", package, "--request", requestPath, "--draft-out", draftPath, "--generated-root", generated, "--vfx-definitions", GlobalVfxDefinitionsPath() });
            Require(mutate == 0 && File.Exists(draftPath), "M2-C12 CLI sequence mutate did not produce a composite apply draft.");
            ProjectileAuthoringDraftBundle composite = JsonSerializer.Deserialize<ProjectileAuthoringDraftBundle>(File.ReadAllText(draftPath), Options)!;
            Require(composite.Documents.Count == 4 && composite.Documents.Count(x => x.Operation == "UpdateConsumer") == 1, "M2-C12 composite draft must include delete, renamed source, duplicate, and consumer exactly once.");

            string failureConsumer = Path.Combine(failurePackage, "config", "test_consumer.json");
            byte[] oldSource = File.ReadAllBytes(Path.Combine(failurePackage, "config", "projectiles", "character_test.fireball.projectile.json"));
            byte[] oldConsumer = File.ReadAllBytes(failureConsumer);
            int failedApply = MxFramework.Authoring.Cli.ProjectilePackageCommands.ApplyForTests(
                new[] { "projectile", "apply", "--package", failurePackage, "--draft", draftPath, "--out", failureGenerated },
                Options,
                new FailAfterFiveCommits());
            Require(failedApply != 0, "M2-C12 injected CLI apply must report failure.");
            Require(File.ReadAllBytes(Path.Combine(failurePackage, "config", "projectiles", "character_test.fireball.projectile.json")).SequenceEqual(oldSource), "M2-C12 source delete was not rolled back.");
            Require(File.ReadAllBytes(failureConsumer).SequenceEqual(oldConsumer), "M2-C12 consumer update was not rolled back.");
            Require(!File.Exists(Path.Combine(failurePackage, "config", "projectiles", "test.fireball.applied.json")) && !File.Exists(Path.Combine(failurePackage, "config", "projectiles", "test.fireball.variant.json")), "M2-C12 created sources survived rollback.");
            string[] artifacts = { "projectile_definitions.json", "projectile_definitions.bytes", "projectile_spawn_profiles.json", "projectile_spawn_profiles.bytes", "projectile_presentation_bindings.json", "projectile_presentation_bindings.bytes", "projectile_compile_report.json" };
            Require(artifacts.All(name => !File.Exists(Path.Combine(failureGenerated, name))), "M2-C12 one or more of seven generated artifacts survived rollback.");

            int apply = MxFramework.Authoring.Cli.Program.Main(new[] { "projectile", "apply", "--package", package, "--draft", draftPath, "--out", generated, "--vfx-definitions", GlobalVfxDefinitionsPath() });
            Require(apply == 0, "M2-A authorized consumer apply failed.");
            using JsonDocument updated = JsonDocument.Parse(File.ReadAllBytes(consumer));
            Require(updated.RootElement.GetProperty("unknown").GetProperty("keep").GetInt32() == 17 && updated.RootElement.GetProperty("request").GetString() == "test.fireball.applied", "M2-A controlled consumer update lost unknown fields or failed to update identity.");
            Require(File.Exists(Path.Combine(package, "config", "projectiles", "test.fireball.variant.json")) && Directory.GetFiles(generated).Length == 7, "M2-C12 real CLI composite apply did not commit source and seven generated artifacts.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class FailAfterFiveCommits : IProjectileAuthoringTransactionFaultInjector
    {
        public void BeforeCommit(int committedItemCount, string receiptPath) { if (committedItemCount == 5) throw new IOException("injected composite rollback"); }
        public void BeforeRollback(string receiptPath) { }
    }

    private sealed class FailAfterTwoCommits : IProjectileAuthoringTransactionFaultInjector
    {
        public void BeforeCommit(int committedItemCount, string receiptPath) { if (committedItemCount == 2) throw new IOException("injected M2 rollback"); }
        public void BeforeRollback(string receiptPath) { }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static ProjectileAuthoringWorkspace LoadWorkspace(string packagePath, string generatedRoot = null) =>
        ProjectileAuthoringWorkspace.Load(packagePath, generatedRoot, GlobalVfxDefinitionsPath(), Options);

    private static string GlobalVfxDefinitionsPath() => Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(),
        "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json"));

    private static string SamplePath() => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Tools/MxFramework.Authoring/samples/ability-character-test"));
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
