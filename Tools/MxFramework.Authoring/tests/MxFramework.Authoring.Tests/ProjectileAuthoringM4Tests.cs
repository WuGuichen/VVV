using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;
using MxFramework.Combat.Projectiles.Unity;

namespace MxFramework.Authoring.Tests;

internal static class ProjectileAuthoringM4Tests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static void RunAll()
    {
        LegacyCompileMapsOnlyFlightAndWritesV2();
        ExplicitMigrationPreservesUnknownAndRemovesDualAuthority();
        SlotValidationAndPresentationHashIsolation();
        WriterRuntimeRoundTripSupportsV1AndV2();
        LegacyCommandsRequireExplicitMigration();
        StableOrderAndSlotContractMatrix();
        ResolutionDuplicateAndLegacyProfileIsolation();
        SynchronizedContractLengthUpdatesAllSourceAuthorities();
        GlobalVfxCatalogResolvesPublishedOneShot();
    }

    private static void GlobalVfxCatalogResolvesPublishedOneShot()
    {
        string root = CopySample();
        try
        {
            string catalogPath = Path.Combine(root, "published_vfx_definitions.json");
            AbilityAuthoringDocument ability = JsonSerializer.Deserialize<AbilityAuthoringDocument>(
                File.ReadAllText(Path.Combine(root, "config", "ability_authoring.json")),
                Options);
            var catalog = new AbilityVfxDefinitionCatalogDocument
            {
                SchemaVersion = "1",
                PackageId = "mxframework.samples",
                Definitions = ability.VfxDefinitions.ToList()
            };
            var published = new VfxDefinitionConfig
            {
                DefinitionId = 20000,
                StableId = "vfx.authored.sample.fireball_impact",
                PrefabResource = new AbilityResourceKeyConfig
                {
                    Id = "vfx.authored.sample.fireball_impact",
                    TypeId = "GameObject",
                    PackageId = "mxframework.samples"
                },
                DefaultLifetimeFrames = 72,
                DefinitionHash = "vfx.authored.sample.fireball_impact.authoring.72"
            };
            catalog.Definitions.Add(published);
            File.WriteAllText(catalogPath, JsonSerializer.Serialize(catalog, Options));

            using ProjectileAuthoringWorkspace workspace = ProjectileAuthoringWorkspace.Load(root, null, catalogPath, Options);
            var request = new ProjectileAuthoringMutationRequest
            {
                Kind = "SetPresentationSlot",
                SourceStableId = "character_test.fireball_burst.projectile",
                PresentationSlotKind = "areaImpact",
                PresentationResource = published.PrefabResource,
                PresentationOrientation = "ImpactNormalOrWorldUp",
                PresentationLifetimeFramesOverride = 72,
                SynchronizeVfxContractLifetime = false,
                PlanOnly = true
            };
            ProjectileAuthoringMutationResult plan = ProjectileAuthoringMutationPlanner.Plan(workspace, request, Options);
            request.PlanOnly = false;
            request.Confirmed = true;
            request.WorkspaceToken = plan.Impact.WorkspaceToken;
            request.ConfirmationToken = plan.Impact.ConfirmationToken;
            ProjectileAuthoringMutationResult result = ProjectileAuthoringMutationPlanner.Plan(workspace, request, Options);

            Require(result.Status == "Ready", "Published global VFX ResourceKey must be accepted by projectile authoring.");
            Require(!result.Diagnostics.Any(value => value.Code == "PROJAUTH011"), "Published global VFX must not report PROJAUTH011.");
            ProjectilePresentationSlotConfig slot = result.ProjectedWorkspace.Bindings
                .Single(value => value.DefinitionStableId == "character_test.fireball_burst.projectile")
                .Detail.Slots.AreaImpact;
            Require(slot.VfxDefinitionId == 20000, "Published global VFX ResourceKey must resolve to compact VfxDefinitionId 20000.");

            string draftPath = Path.Combine(root, "published-vfx.draft.json");
            string generatedPath = Path.Combine(root, "generated-published-vfx");
            File.WriteAllText(draftPath, JsonSerializer.Serialize(result.Draft, Options));
            int applied = MxFramework.Authoring.Cli.ProjectilePackageCommands.ApplyForTests(
                new[]
                {
                    "projectile", "apply",
                    "--package", root,
                    "--draft", draftPath,
                    "--out", generatedPath,
                    "--vfx-definitions", catalogPath
                },
                Options,
                null);
            Require(applied == 0, "Save-stage transaction must commit the published global VFX selection.");

            using ProjectileAuthoringWorkspace reloaded = ProjectileAuthoringWorkspace.Load(root, null, catalogPath, Options);
            ProjectileAuthoringCompileResult compiled = reloaded.Compile();
            Require(
                !compiled.HasBlockingIssues,
                "Save-stage compile must accept the published global VFX ResourceKey: "
                + string.Join(" | ", compiled.Diagnostics.Where(value => string.Equals(value.Severity, "Error", StringComparison.OrdinalIgnoreCase)).Select(value => value.Code + ":" + value.Message)));
            Require(!compiled.Diagnostics.Any(value => value.Code == "PROJAUTH012"), "Save-stage compile must not report the published global VFX ResourceKey as unknown.");
            ProjectilePresentationSlotConfig compiledSlot = compiled.PresentationBindings.Bindings
                .Single(value => value.ProjectileDefinitionId == 91002)
                .Slots.AreaImpact;
            Require(compiledSlot.VfxDefinitionId == 20000, "Save-stage compile must retain compact VfxDefinitionId 20000.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void WriterRuntimeRoundTripSupportsV1AndV2()
    {
        var legacy = new AbilityProjectilePresentationBindingDocument
        {
            SchemaVersion = "1",
            PackageId = "fixture",
            Bindings = new List<ProjectilePresentationBindingConfig>
            {
                new ProjectilePresentationBindingConfig { ProjectileDefinitionId = 7, PrefabResource = new AbilityResourceKeyConfig { Id = "vfx.fixture.flight", TypeId = "GameObject", PackageId = "fixture" }, ViewNamePrefix = "Legacy" }
            }
        };
        var legacyResult = CombatProjectileUnityPresentationConfigBinaryImporter.ImportBytes(RuntimeCatalogBinaryArtifacts.WriteProjectilePresentationBindings(legacy));
        CombatProjectileUnityPresentationBindingV2 legacyBinding = null;
        Require(legacyResult.Success && legacyResult.Catalog.TryGetV2(7, out legacyBinding), "M4-C08: writer v1 payload must round-trip through the runtime v1 importer.");
        Require(legacyBinding.TryGetSlot(CombatProjectileUnityPresentationSlotKind.FlightBody, out _) && !legacyBinding.TryGetSlot(CombatProjectileUnityPresentationSlotKind.DirectImpact, out _), "M4-C08: writer v1 round-trip must map only flightBody.");

        var v2 = new AbilityProjectilePresentationBindingDocument
        {
            SchemaVersion = "2",
            PackageId = "fixture",
            Bindings = new List<ProjectilePresentationBindingConfig>
            {
                new ProjectilePresentationBindingConfig
                {
                    ProjectileDefinitionId = 8,
                    ViewNamePrefix = "V2",
                    Slots = new ProjectilePresentationSlotSetConfig
                    {
                        Spawn = Slot("vfx.fixture.spawn", "WorldUp", 101),
                        FlightBody = Slot("vfx.fixture.flight", "ProjectileForward", 0),
                        Trail = Slot("vfx.fixture.trail", "ProjectileForward", 0),
                        DirectImpact = Slot("vfx.fixture.direct", "SurfaceNormal", 102),
                        AreaImpact = Slot("vfx.fixture.area", "ImpactNormalOrWorldUp", 103),
                        BlockedImpact = Slot("vfx.fixture.blocked", "SurfaceNormal", 104),
                        Expire = Slot("vfx.fixture.expire", "WorldUp", 105)
                    },
                    ExitPolicy = new ProjectilePresentationExitPolicyConfig()
                }
            }
        };
        var v2Result = CombatProjectileUnityPresentationConfigBinaryImporter.ImportBytes(RuntimeCatalogBinaryArtifacts.WriteProjectilePresentationBindings(v2));
        CombatProjectileUnityPresentationBindingV2 v2Binding = null;
        Require(v2Result.Success && v2Result.Catalog.TryGetV2(8, out v2Binding), "M4-C08: writer v2 payload must round-trip through the runtime v2 importer.");
        foreach (CombatProjectileUnityPresentationSlotKind kind in Enum.GetValues<CombatProjectileUnityPresentationSlotKind>()) Require(v2Binding.TryGetSlot(kind, out _), "M4-C08: writer v2 round-trip must preserve all seven slots.");
        bool unknownRejected = false;
        try { RuntimeCatalogBinaryArtifacts.WriteProjectilePresentationBindings(new AbilityProjectilePresentationBindingDocument { SchemaVersion = "9" }); }
        catch (InvalidDataException) { unknownRejected = true; }
        Require(unknownRejected, "M4-C08: writer must reject unknown presentation schemas.");
    }

    private static ProjectilePresentationSlotConfig Slot(string id, string orientation, int vfxDefinitionId) => new ProjectilePresentationSlotConfig
    {
        Resource = new AbilityResourceKeyConfig { Id = id, TypeId = "GameObject", PackageId = "fixture" },
        Orientation = orientation,
        LocalOffset = new ProjectileVector3Config(),
        VfxDefinitionId = vfxDefinitionId
    };

    private static void StableOrderAndSlotContractMatrix()
    {
        ProjectilePresentationBindingConfig first = new ProjectilePresentationBindingConfig
        {
            ProjectileDefinitionId = 2,
            Slots = new ProjectilePresentationSlotSetConfig { FlightBody = Slot("vfx.fixture.flight", "ProjectileForward", 0) },
            ExitPolicy = new ProjectilePresentationExitPolicyConfig()
        };
        ProjectilePresentationBindingConfig second = new ProjectilePresentationBindingConfig
        {
            ProjectileDefinitionId = 1,
            Slots = new ProjectilePresentationSlotSetConfig { DirectImpact = Slot("vfx.fixture.impact", "SurfaceNormal", 12) },
            ExitPolicy = new ProjectilePresentationExitPolicyConfig()
        };
        byte[] forward = RuntimeCatalogBinaryArtifacts.WriteProjectilePresentationBindings(new AbilityProjectilePresentationBindingDocument
        {
            SchemaVersion = "2", PackageId = "fixture", Bindings = new List<ProjectilePresentationBindingConfig> { first, second }
        });
        byte[] reverse = RuntimeCatalogBinaryArtifacts.WriteProjectilePresentationBindings(new AbilityProjectilePresentationBindingDocument
        {
            SchemaVersion = "2", PackageId = "fixture", Bindings = new List<ProjectilePresentationBindingConfig> { second, first }
        });
        Require(forward.SequenceEqual(reverse), "M4-C03: binding input order must not change canonical binary bytes/hash.");

        var valid = new Dictionary<string, string[]>
        {
            ["spawn"] = new[] { "ProjectileForward", "WorldUp" },
            ["flightBody"] = new[] { "ProjectileForward" },
            ["trail"] = new[] { "ProjectileForward" },
            ["directImpact"] = new[] { "SurfaceNormal", "ProjectileForward" },
            ["areaImpact"] = new[] { "ImpactNormalOrWorldUp", "WorldUp" },
            ["blockedImpact"] = new[] { "SurfaceNormal" },
            ["expire"] = new[] { "ProjectileForward", "WorldUp" }
        };
        foreach (KeyValuePair<string, string[]> pair in valid)
        {
            foreach (string orientation in pair.Value)
                Require(InvokeOrientationAllowed(pair.Key, orientation), "M4-C04: valid slot/orientation pair was rejected: " + pair.Key + "/" + orientation);
            Require(!InvokeOrientationAllowed(pair.Key, "InvalidOrientation"), "M4-C04: invalid orientation must be rejected for " + pair.Key + ".");
        }
    }

    private static bool InvokeOrientationAllowed(string kind, string orientation)
    {
        System.Reflection.MethodInfo method = typeof(ProjectileAuthoringPresentationEditor).GetMethod("OrientationAllowed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return method != null && (bool)method.Invoke(null, new object[] { kind, orientation });
    }

    private static void ResolutionDuplicateAndLegacyProfileIsolation()
    {
        string root = CopySample();
        try
        {
            string sourcePath = Path.Combine(root, "config", "projectiles", "character_test.fireball.projectile.json");
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath)).AsObject();
            ((JsonObject)((JsonObject)source["definition"])["impact"])["presentationProfileId"] = "legacy.profile.manual";
            source["m5UnknownPreserved"] = new JsonObject { ["sentinel"] = 17 };
            File.WriteAllText(sourcePath, source.ToJsonString(Options) + Environment.NewLine);

            using (ProjectileAuthoringWorkspace unchangedWorkspace = LoadWorkspace(root))
            {
                ProjectileAuthoringSourceDocument unchanged = unchangedWorkspace.SourceDocuments.Single(value => value.RelativePath.EndsWith("character_test.fireball.projectile.json", StringComparison.Ordinal));
                Require(unchangedWorkspace.TryBuildLosslessProposal(unchanged, unchanged.PreservedJson.RootElement, out byte[] unchangedBytes, out _),
                    "M4-C05: unchanged source must remain a valid lossless proposal.");
                Require(unchangedBytes.SequenceEqual(unchanged.OriginalUtf8Bytes),
                    "M4-C05: unchanged source must preserve exact bytes and must not become dirty.");
            }

            ProjectileAuthoringMutationResult migration = ApplyMutation(root, new ProjectileAuthoringMutationRequest
            {
                Kind = "MigratePresentationBindingV1ToV2",
                SourceStableId = "character_test.fireball.projectile"
            });
            Require(migration.Diagnostics.Any(value => value.Code == "PROJAUTH014"),
                "M4-C12: migration must report legacy presentationProfileId as an explicit manual-migration diagnostic.");
            var resource = new AbilityResourceKeyConfig
            {
                Id = "vfx.magic.fireball", TypeId = "GameObject", PackageId = "mxframework.samples"
            };
            Require(PlanConfirmed(root, new ProjectileAuthoringMutationRequest
            {
                Kind = "SetPresentationSlot", SourceStableId = "character_test.fireball.projectile",
                PresentationSlotKind = "flightBody", PresentationResource = resource,
                PresentationOrientation = "ProjectileForward", PresentationLifetimeFramesOverride = 1
            }).Diagnostics.Any(value => value.Code == "PROJAUTH012"),
                "M4-C04: persistent slot lifetime override must be zero.");
            Require(PlanConfirmed(root, new ProjectileAuthoringMutationRequest
            {
                Kind = "SetPresentationSlot", SourceStableId = "character_test.fireball.projectile",
                PresentationSlotKind = "spawn", PresentationResource = resource,
                PresentationOrientation = "WorldUp", PresentationLifetimeFramesOverride = -1
            }).Diagnostics.Any(value => value.Code == "PROJAUTH012"),
                "M4-C04: one-shot lifetime override must reject negative values.");
            Require(PlanConfirmed(root, new ProjectileAuthoringMutationRequest
            {
                Kind = "SetPresentationSlot", SourceStableId = "character_test.fireball.projectile",
                PresentationSlotKind = "spawn", PresentationResource = new AbilityResourceKeyConfig { Id = "bad", TypeId = "Texture" },
                PresentationOrientation = "WorldUp"
            }).Diagnostics.Any(value => value.Code == "PROJAUTH012"),
                "M4-C04: slot ResourceKey type must be GameObject.");
            Require(PlanConfirmed(root, new ProjectileAuthoringMutationRequest
            {
                Kind = "EditPresentationExitPolicy", SourceStableId = "character_test.fireball.projectile",
                PresentationExitKind = "acceptedImpact", PresentationExitMode = "Immediate"
            }).Diagnostics.Any(value => value.Code == "PROJAUTH012"),
                "M4-C04: ordinary exits must reject Immediate; runtimeReset remains fixed Immediate.");
            ProjectileAuthoringMutationResult directImpactMutation = ApplyMutation(root, new ProjectileAuthoringMutationRequest
            {
                Kind = "SetPresentationSlot", SourceStableId = "character_test.fireball.projectile",
                PresentationSlotKind = "directImpact", PresentationResource = resource,
                PresentationOrientation = "SurfaceNormal"
            });
            ProjectilePresentationBindingConfig projectedBinding = directImpactMutation.ProjectedWorkspace.Bindings
                .Single(value => value.ProjectileDefinitionId == 91001).Detail;
            Require(projectedBinding.Slots.DirectImpact.VfxDefinitionId > 0,
                "M4-C05 staged presentation projection must expose the compiler-resolved one-shot VfxDefinitionId.");
            ApplyMutation(root, new ProjectileAuthoringMutationRequest
            {
                Kind = "SetPresentationSlot", SourceStableId = "character_test.fireball.projectile",
                PresentationSlotKind = "spawn", PresentationResource = resource,
                PresentationOrientation = "WorldUp"
            });
            string canonicalSource = File.ReadAllText(sourcePath);
            JsonObject canonicalRoot = JsonNode.Parse(canonicalSource).AsObject();
            JsonObject canonicalResource = ((JsonObject)((JsonObject)((JsonObject)canonicalRoot["presentationBinding"])["slots"])["spawn"])["resource"].AsObject();
            Require(canonicalResource.ContainsKey("id") && canonicalResource.ContainsKey("typeId") && canonicalResource.ContainsKey("packageId"),
                "M4-C05: formal SetPresentationSlot transaction must persist canonical camelCase ResourceKey fields.");
            Require(!canonicalSource.Contains("\"Id\"", StringComparison.Ordinal)
                && !canonicalSource.Contains("\"TypeId\"", StringComparison.Ordinal)
                && !canonicalSource.Contains("\"PackageId\"", StringComparison.Ordinal),
                "M4-C05: formal SetPresentationSlot transaction must not persist PascalCase ResourceKey aliases.");
            Require(canonicalSource.EndsWith(Environment.NewLine, StringComparison.Ordinal),
                "M4-C05: formal projectile source transaction must preserve a trailing newline.");
            Require(JsonNode.Parse(canonicalSource).AsObject()["m5UnknownPreserved"]?["sentinel"]?.GetValue<int>() == 17,
                "M4-C05: formal projectile source transaction must preserve unknown JSON fields.");

            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            ProjectileAuthoringCompileResult compiled = workspace.Compile();
            ProjectilePresentationBindingConfig binding = compiled.PresentationBindings.Bindings.Single(value => value.ProjectileDefinitionId == 91001);
            Require(binding.Slots.Spawn.VfxDefinitionId > 0 && binding.Slots.DirectImpact.VfxDefinitionId > 0,
                "M4-C05: one-shot slots must resolve compact VfxDefinitionId values.");
            Require(binding.Slots.FlightBody.VfxDefinitionId == 0,
                "M4-C05: persistent flight/trail slots must retain ResourceKey and omit compact one-shot ids.");
            Require(compiled.Diagnostics.Any(value => value.Code == "PROJAUTH015"),
                "M4-C06: duplicate same-frame one-shot ResourceKey must emit the deterministic warning.");
            Require(!JsonSerializer.Serialize(compiled.PresentationBindings, Options).Contains("presentationProfileId", StringComparison.Ordinal),
                "M4-C12: legacy presentationProfileId must never enter the v2 presentation artifact.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void SynchronizedContractLengthUpdatesAllSourceAuthorities()
    {
        string root = CopySample();
        try
        {
            ProjectileAuthoringMutationResult result = PlanConfirmed(root, new ProjectileAuthoringMutationRequest
            {
                Kind = "SetPresentationSlot",
                SourceStableId = "character_test.fireball_burst.projectile",
                PresentationSlotKind = "areaImpact",
                PresentationResource = new AbilityResourceKeyConfig
                {
                    Id = "vfx.magic.fireball_burst.impact",
                    TypeId = "GameObject",
                    PackageId = "mxframework.samples"
                },
                PresentationOrientation = "ImpactNormalOrWorldUp",
                PresentationLifetimeFramesOverride = 72,
                SynchronizeVfxContractLifetime = true
            });
            Require(result.Status == "Ready" && result.Draft.Documents.Count == 2,
                "M4-C13: synchronized contract edit must produce one projectile and one VFX contract proposal.");
            Require(result.Draft.Documents.Any(value =>
                    value.Operation == "SynchronizeVfxContract"
                    && value.SourceRelativePath == AbilityAuthoringCompiler.AbilityAuthoringDocumentRelativePath),
                "M4-C13: synchronized contract edit must use the dedicated ability-authoring proposal.");

            string draftPath = Path.Combine(root, "contract-lifetime.draft.json");
            string generatedPath = Path.Combine(root, "generated-contract-lifetime");
            File.WriteAllText(draftPath, JsonSerializer.Serialize(result.Draft, Options));
            int applied = MxFramework.Authoring.Cli.ProjectilePackageCommands.ApplyForTests(
                new[]
                {
                    "projectile", "apply", "--package", root, "--draft", draftPath, "--out", generatedPath,
                    "--vfx-definitions", Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json"))
                },
                Options,
                null);
            Require(applied == 0,
                "M4-C13: synchronized contract edit must apply both source documents atomically.");

            JsonObject ability = JsonNode.Parse(File.ReadAllBytes(
                Path.Combine(root, "config", "ability_authoring.json"))).AsObject();
            JsonObject definition = ability["vfxDefinitions"].AsArray().OfType<JsonObject>().Single(
                value => value["definitionId"].GetValue<int>() == 12002);
            JsonObject profile = ability["vfxPresentationProfiles"].AsArray().OfType<JsonObject>().Single(
                value => (value["presentationProfileLabel"]?.GetValue<string>() ?? value["presentationProfileId"]?.ToString()) == "character_test.fireball_burst.impact");
            JsonObject projectile = JsonNode.Parse(File.ReadAllBytes(Path.Combine(
                root, "config", "projectiles", "character_test.fireball_burst.projectile.json"))).AsObject();
            int slotFrames = projectile["presentationBinding"]["slots"]["areaImpact"]["lifetimeFramesOverride"].GetValue<int>();

            Require(definition["defaultLifetimeFrames"].GetValue<int>() == 72,
                "M4-C13: synchronized contract edit must update the VFX definition lifetime.");
            Require(profile["lifetimeFramesOverride"].GetValue<int>() == 72,
                "M4-C13: synchronized contract edit must update the presentation profile lifetime.");
            Require(slotFrames == 72,
                "M4-C13: synchronized contract edit must update the projectile presentation slot lifetime.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static ProjectileAuthoringMutationResult ApplyMutation(string root, ProjectileAuthoringMutationRequest request)
    {
        ProjectileAuthoringMutationResult result = PlanConfirmed(root, request);
        Require(result.Status == "Ready" && result.Draft.Documents.Count == 1,
            "M4 fixture mutation failed: " + request.Kind + "/" + request.PresentationSlotKind);
        ProjectileAuthoringDraftDocument document = result.Draft.Documents.Single();
        using (ProjectileAuthoringWorkspace workspace = LoadWorkspace(root))
        {
            ProjectileAuthoringSourceDocument source = workspace.SourceDocuments.Single(value =>
                string.Equals(value.RelativePath, document.SourceRelativePath, StringComparison.Ordinal));
            Require(workspace.TryBuildLosslessProposal(source, document.Json, out byte[] content, out ProjectileAuthoringDiagnostic diagnostic),
                "M4 formal source proposal failed: " + diagnostic?.Code);
            File.WriteAllBytes(Path.Combine(root, document.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar)), content);
        }
        return result;
    }

    private static ProjectileAuthoringMutationResult PlanConfirmed(string root, ProjectileAuthoringMutationRequest request)
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
        request.PlanOnly = true;
        ProjectileAuthoringMutationResult plan = ProjectileAuthoringMutationPlanner.Plan(workspace, request, Options);
        request.PlanOnly = false;
        request.Confirmed = true;
        request.WorkspaceToken = plan.Impact.WorkspaceToken;
        request.ConfirmationToken = plan.Impact.ConfirmationToken;
        return ProjectileAuthoringMutationPlanner.Plan(workspace, request, Options);
    }

    private static void LegacyCommandsRequireExplicitMigration()
    {
        string root = CopySample();
        try
        {
            string path = Path.Combine(root, "config", "projectiles", "character_test.fireball.projectile.json");
            byte[] before = File.ReadAllBytes(path);
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            ProjectileAuthoringMutationRequest[] blocked =
            {
                new ProjectileAuthoringMutationRequest { Kind = "SetPresentationSlot", SourceStableId = "character_test.fireball.projectile", PresentationSlotKind = "trail", PresentationResource = new AbilityResourceKeyConfig { Id = "vfx.magic.fireball", TypeId = "GameObject", PackageId = "mxframework.samples" }, PresentationOrientation = "ProjectileForward" },
                new ProjectileAuthoringMutationRequest { Kind = "ClearPresentationSlot", SourceStableId = "character_test.fireball.projectile", PresentationSlotKind = "flightBody" },
                new ProjectileAuthoringMutationRequest { Kind = "EditPresentationExitPolicy", SourceStableId = "character_test.fireball.projectile", PresentationExitKind = "acceptedImpact", PresentationExitMode = "Dissipate" }
            };
            foreach (ProjectileAuthoringMutationRequest request in blocked)
            {
                ProjectileAuthoringMutationResult result = ProjectileAuthoringMutationPlanner.Plan(workspace, request, Options);
                Require(result.Draft.Documents.Count == 0 && result.Diagnostics.Any(value => value.Code == "PROJAUTH013"), "M4-C02: every non-migrate legacy presentation command must be blocked without a draft.");
                Require(File.ReadAllBytes(path).SequenceEqual(before), "M4-C02: blocked legacy commands must not change original bytes.");
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private static void LegacyCompileMapsOnlyFlightAndWritesV2()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SampleRoot());
        ProjectileAuthoringCompileResult compiled = workspace.Compile();
        Require(compiled.PresentationBindings.SchemaVersion == "2", "M4-C01/C08: writer must emit presentation schema v2.");
        ProjectilePresentationBindingConfig binding = compiled.PresentationBindings.Bindings.First(value => value.ProjectileDefinitionId == 91001);
        Require(binding.Slots?.FlightBody?.Resource?.Id == "vfx.magic.fireball", "M4-C01: legacy prefab must map to flightBody.");
        Require(binding.Slots.Spawn == null && binding.Slots.DirectImpact == null && binding.Slots.Expire == null, "M4-C01/R03: legacy mapping must not infer one-shot slots.");
        string json = JsonSerializer.Serialize(compiled.PresentationBindings, Options);
        Require(!json.Contains("prefabResource", StringComparison.Ordinal), "M4-C01: v2 writer must not retain legacy dual authority.");
    }

    private static void ExplicitMigrationPreservesUnknownAndRemovesDualAuthority()
    {
        string root = CopySample();
        try
        {
            string path = Path.Combine(root, "config", "projectiles", "character_test.fireball.projectile.json");
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(path)).AsObject();
            ((JsonObject)source["presentationBinding"])["m4Unknown"] = new JsonObject { ["keep"] = 17 };
            File.WriteAllText(path, source.ToJsonString(Options));
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            var preview = new ProjectileAuthoringMutationRequest { Kind = "MigratePresentationBindingV1ToV2", SourceStableId = "character_test.fireball.projectile", PlanOnly = true };
            ProjectileAuthoringMutationResult plan = ProjectileAuthoringMutationPlanner.Plan(workspace, preview, Options);
            var apply = new ProjectileAuthoringMutationRequest { Kind = preview.Kind, SourceStableId = preview.SourceStableId, Confirmed = true, ConfirmationToken = plan.Impact.ConfirmationToken, WorkspaceToken = plan.Impact.WorkspaceToken };
            ProjectileAuthoringMutationResult result = ProjectileAuthoringMutationPlanner.Plan(workspace, apply, Options);
            Require(result.Status == "Ready" && result.Draft.Documents.Count == 1, "M4-C02/C11: migration must be one atomic history command.");
            JsonElement binding = result.Draft.Documents[0].Json.GetProperty("presentationBinding");
            Require(!binding.TryGetProperty("prefabResource", out _), "M4-C02: migration must delete legacy dual authority.");
            Require(binding.GetProperty("slots").GetProperty("flightBody").GetProperty("resource").GetProperty("id").GetString() == "vfx.magic.fireball", "M4-C02: migration must preserve exact flight resource.");
            Require(binding.GetProperty("m4Unknown").GetProperty("keep").GetInt32() == 17, "M4-C02: migration must preserve unknown siblings.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void SlotValidationAndPresentationHashIsolation()
    {
        string root = CopySample();
        try
        {
            using (ProjectileAuthoringWorkspace legacyWorkspace = LoadWorkspace(root))
            {
                ProjectileAuthoringMutationResult migration = ProjectileAuthoringMutationPlanner.Plan(
                    legacyWorkspace,
                    new ProjectileAuthoringMutationRequest
                    {
                        Kind = "MigratePresentationBindingV1ToV2",
                        SourceStableId = "character_test.fireball.projectile",
                        PlanOnly = false
                    },
                    Options);
                Require(migration.Status == "Ready", "M4-C04: fixture migration must succeed before v2 slot validation.");
                ProjectileAuthoringDraftDocument migrated = migration.Draft.Documents.Single();
                File.WriteAllText(Path.Combine(root, migrated.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar)), migrated.Json.GetRawText());
            }
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            ProjectileAuthoringCompileResult before = workspace.Compile();
            string definitions = JsonSerializer.Serialize(before.Definitions, Options);
            byte[] definitionBytes = RuntimeCatalogBinaryArtifacts.WriteProjectileDefinitions(before.Definitions);
            string definitionJsonHash = Hash(System.Text.Encoding.UTF8.GetBytes(definitions));
            string definitionBinaryHash = Hash(definitionBytes);
            string presentation = JsonSerializer.Serialize(before.PresentationBindings, Options);
            var preview = new ProjectileAuthoringMutationRequest
            {
                Kind = "SetPresentationSlot", SourceStableId = "character_test.fireball.projectile", PresentationSlotKind = "trail",
                PresentationResource = new AbilityResourceKeyConfig { Id = "vfx.magic.fireball", TypeId = "GameObject", PackageId = "mxframework.samples" },
                PresentationOrientation = "SurfaceNormal", PlanOnly = false
            };
            ProjectileAuthoringMutationResult invalid = ProjectileAuthoringMutationPlanner.Plan(workspace, preview, Options);
            Require(invalid.Diagnostics.Any(value => value.Code == "PROJAUTH012"), "M4-C04: incompatible slot orientation must block.");
            preview.PresentationOrientation = "ProjectileForward";
            ProjectileAuthoringMutationResult valid = ProjectileAuthoringMutationPlanner.Plan(workspace, preview, Options);
            Require(valid.Status == "Ready", "M4-C04: valid persistent slot must be accepted.");
            ProjectileAuthoringDraftDocument changed = valid.Draft.Documents.Single();
            File.WriteAllText(Path.Combine(root, changed.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar)), changed.Json.GetRawText());
            using ProjectileAuthoringWorkspace reloaded = LoadWorkspace(root);
            ProjectileAuthoringCompileResult after = reloaded.Compile();
            Require(JsonSerializer.Serialize(after.Definitions, Options) == definitions, "M4-C07: an applied presentation-only edit must not alter the Combat Definition artifact.");
            byte[] afterDefinitionBytes = RuntimeCatalogBinaryArtifacts.WriteProjectileDefinitions(after.Definitions);
            Require(afterDefinitionBytes.SequenceEqual(definitionBytes), "M4-C07: an applied presentation-only edit must not alter Definition binary bytes.");
            Require(Hash(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(after.Definitions, Options))) == definitionJsonHash, "M4-C07: an applied presentation-only edit must not alter Definition JSON hash.");
            Require(Hash(afterDefinitionBytes) == definitionBinaryHash, "M4-C07: an applied presentation-only edit must not alter Definition binary hash.");
            Require(JsonSerializer.Serialize(after.PresentationBindings, Options) != presentation, "M4-C07: the Presentation artifact must change when a lifecycle slot changes.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static ProjectileAuthoringWorkspace LoadWorkspace(string packagePath) =>
        ProjectileAuthoringWorkspace.Load(
            packagePath,
            null,
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json")),
            Options);

    private static string SampleRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "ability-character-test"));

    private static string CopySample()
    {
        string target = Path.Combine(Path.GetTempPath(), "mx-projectile-m4-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(SampleRoot(), target);
        return target;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(directory.Replace(source, target));
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(source, target), true);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string Hash(byte[] value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(value));
}
