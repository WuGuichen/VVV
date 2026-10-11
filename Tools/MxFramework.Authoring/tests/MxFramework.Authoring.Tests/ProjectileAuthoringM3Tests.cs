using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

internal static class ProjectileAuthoringM3Tests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static void RunAll()
    {
        ProjectionIncludesTypedDetailsRawNodesAndSockets();
        DefinitionEditsAreTypedAtomicAndUnknownPreserving();
        MotionImpactListsAndDamageRemainIndependent();
        CompoundTypeChangesApplyStableDefaults();
        DormantAreaFieldsFallBackWithoutLosingUnknowns();
        RequestEditsValidateSocketAimAndProjection();
        RequestCompilerDefaultsRemainEditable();
        FieldEditsParticipateInOrderedSequence();
        FieldEditDraftAppliesAndRollsBackAtomically();
        DiagnosticsCover025Through029AndSharedConfirmation();
        CompilerCompatiblePhysicalAndInheritedAreaCasesRemainEditable();
    }

    private static void ProjectionIncludesTypedDetailsRawNodesAndSockets()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        ProjectileAuthoringWorkspaceProjection projection = ProjectileAuthoringWorkspaceProjectionBuilder.Build(workspace, workspace.Compile());
        ProjectileAuthoringWorkspaceDefinition definition = projection.Definitions.First(value => value.StableId == "character_test.fireball.projectile");
        ProjectileAuthoringWorkspaceRequest request = projection.Requests.First(value => value.StableId == "character_test.fireball.projectile");
        ProjectileAuthoringWorkspaceBinding burstBinding = projection.Bindings.First(value => value.DefinitionStableId == "character_test.fireball_burst.projectile");
        using JsonDocument definitionRaw = JsonDocument.Parse(definition.RawJson);
        using JsonDocument requestRaw = JsonDocument.Parse(request.RawJson);
        Require(definition.Detail?.Motion?.Kind == "Linear" && definitionRaw.RootElement.GetProperty("definitionId").GetInt32() == 91001, "M3-C15 definition typed detail/raw projection is missing.");
        Require(request.Detail?.Origin?.SocketLabel == "mainHand" && requestRaw.RootElement.GetProperty("origin").GetProperty("socketLabel").GetString() == "mainHand", "M3-C02/C15 request typed detail/raw projection is missing.");
        Require(projection.KnownSocketIds.SequenceEqual(new[] { "mainHand" }), "M3-C02 knownSocketIds projection is inaccurate.");
        Require(burstBinding.Detail?.Slots?.AreaImpact?.VfxDefinitionId == 12002,
            "M3-C15 workspace projection must expose the compiler-resolved authored one-shot VfxDefinitionId.");
        Require(burstBinding.Detail.Slots.Trail == null && burstBinding.Detail.Slots.DirectImpact == null,
            "M3-C15 workspace projection must preserve absent lifecycle slots as unconfigured.");
    }

    private static void DefinitionEditsAreTypedAtomicAndUnknownPreserving()
    {
        string root = CopySample();
        try
        {
            string path = Path.Combine(root, "config", "projectiles", "character_test.fireball.projectile.json");
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(path)).AsObject();
            source["unknownRoot"] = new JsonObject { ["keep"] = 17 };
            source["definition"]["motion"]["unknownMotion"] = "opaque";
            File.WriteAllText(path, source.ToJsonString());
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            ProjectileAuthoringMutationResult result = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile",
                Float("motion.speedPerFrame", .75f), Bool("physicalEventsEnabled", true), Enum("motion.speedProfile.kind", "EaseInOutSmoothstep"));
            Require(result.Status == "Ready" && result.Draft.Documents.Count == 1, "M3-C01 definition field batch should be ready.");
            JsonElement output = result.Draft.Documents[0].Json;
            Require(output.GetProperty("definition").GetProperty("motion").GetProperty("speedPerFrame").GetSingle() == .75f, "M3-C01 float edit was not projected.");
            Require(output.GetProperty("unknownRoot").GetProperty("keep").GetInt32() == 17 && output.GetProperty("definition").GetProperty("motion").GetProperty("unknownMotion").GetString() == "opaque", "M3-C13 unknown JSON was not preserved.");
            Require(result.ProjectedWorkspace.Definitions.First(value => value.StableId == "character_test.fireball.projectile").Detail.Motion.SpeedProfile.Kind == "EaseInOutSmoothstep", "M3-C15 projected typed detail is stale.");

            ProjectileAuthoringMutationResult atomic = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Float("motion.speedPerFrame", .9f), String("collision.radius", "bad"));
            Require(atomic.Status == "Blocked" && atomic.Draft.Documents.Count == 0 && atomic.Diagnostics.Any(value => value.Code == "PROJAUTH026"), "M3-C12 invalid field must reject the entire batch.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void MotionImpactListsAndDamageRemainIndependent()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        ProjectileAuthoringMutationResult ballistic = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile",
            Enum("motion.kind", "BallisticArcToTarget"), Float("motion.gravityPerFrameSquared", .01f), Float("motion.maxLaunchAngleDegrees", 45f));
        Require(ballistic.Status == "Ready", "M3-C04 ballistic switch should be valid.");
        JsonElement motion = ballistic.Draft.Documents[0].Json.GetProperty("definition").GetProperty("motion");
        Require(motion.GetProperty("maxLaunchAngleTangent").GetSingle() > .99f && motion.GetProperty("maxLaunchAngleTangent").GetSingle() < 1.01f, "M3-C04 ballistic tangent normalization is incorrect.");

        ProjectileAuthoringMutationResult direct = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile",
            Int("impact.hitEvent.damage.baseDamage", 21), Strings("impact.hitEvent.damage.tags", "fire", "direct"), Ints("impact.hitEvent.onHitBuffs", 7, 3));
        Require(direct.Status == "Ready", "M3-C08/C10 direct damage/list edits should be ready.");
        JsonElement hit = direct.Draft.Documents[0].Json.GetProperty("definition").GetProperty("impact").GetProperty("hitEvent");
        Require(hit.GetProperty("damage").GetProperty("baseDamage").GetInt32() == 21 && hit.GetProperty("onHitBuffs")[0].GetProperty("buffId").GetInt32() == 7, "M3-C08/C10 direct damage or ordered buffs were not retained.");

        ProjectileAuthoringMutationResult area = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile",
            Enum("impact.policyKind", "AreaPulseOnImpact"), Float("impact.areaRadius", 2f), Strings("impact.areaHitEvent.targetTypes", "WorldGround", "HostileCharacter"), String("impact.areaHitEvent.damage.damageTypeId", "arcane"), Int("impact.areaHitEvent.damage.baseDamage", 5), String("impact.areaHitEvent.environmentHitPolicy", "None", "Enum"), String("impact.areaHitEvent.hitSameTargetPolicy", "AllowRepeat", "Enum"), Int("impact.areaHitEvent.maxAcceptedHits", 3));
        Require(area.Status == "Ready", "M3-C07/C08 area impact should create independent area hit data.");
        JsonElement impact = area.Draft.Documents[0].Json.GetProperty("definition").GetProperty("impact");
        Require(impact.GetProperty("hitEvent").GetProperty("damage").GetProperty("baseDamage").GetInt32() == 10 && impact.GetProperty("areaHitEvent").GetProperty("damage").GetProperty("baseDamage").GetInt32() == 5, "M3-C08 direct and area damage overwrote one another.");
        Require(!impact.TryGetProperty("areaLayerMask", out _) && area.ProjectedWorkspace.Definitions.First(value => value.StableId == "character_test.fireball.projectile").Detail.Impact.AreaLayerMask == ((1 << 1) | (1 << 8)), "M3-C06/C09 raw area layer mask must remain absent while the projected derived mask stays accurate.");
    }

    private static void RequestEditsValidateSocketAimAndProjection()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        ProjectileAuthoringMutationResult result = Plan(workspace, "EditRequestFields", "character_test.fireball.projectile",
            Enum("origin.kind", "Socket"), Enum("aim.kind", "CasterForwardRange"), Enum("aim.fallbackKind", "CasterForwardRange"), Float("aim.maxRange", 33f), Vector("origin.localOffset", 1, 2, 3), String("origin.socketId", "mainHand"));
        Require(result.Status == "Ready", "M3-C02/C03 valid request edit should be ready.");
        ProjectileSpawnProfileConfig detail = result.ProjectedWorkspace.Requests.First(value => value.StableId == "character_test.fireball.projectile").Detail;
        Require(detail.Aim.Kind == "CasterForwardRange" && detail.Aim.TargetHeightOffset == 0f && detail.Origin.LocalOffset.Y == 2f, "M3-C03 request normalization/projection is incorrect.");
        ProjectileAuthoringMutationResult badSocket = Plan(workspace, "EditRequestFields", "character_test.fireball.projectile", String("origin.socketId", "not-authoritative"));
        Require(badSocket.Status == "Blocked" && badSocket.Diagnostics.Any(value => value.Code == "PROJAUTH028" && value.FieldPath == "origin.socketId"), "M3-C02 unknown socket should fail with a field diagnostic.");
        ProjectileAuthoringWorkspaceProjection before = ProjectileAuthoringWorkspaceProjectionBuilder.Build(workspace, workspace.Compile());
        ProjectileAuthoringWorkspaceDefinition oldBefore = before.Definitions.First(value => value.StableId == "character_test.fireball.projectile");
        ProjectileAuthoringWorkspaceDefinition newBefore = before.Definitions.First(value => value.StableId == "character_test.fireball_burst.projectile");
        ProjectileAuthoringMutationResult reassigned = Plan(workspace, "EditRequestFields", "character_test.fireball.projectile", String("projectileStableId", "character_test.fireball_burst.projectile"));
        ProjectileAuthoringWorkspaceRequest reassignedRequest = reassigned.ProjectedWorkspace.Requests.First(value => value.StableId == "character_test.fireball.projectile");
        ProjectileAuthoringWorkspaceDefinition oldAfter = reassigned.ProjectedWorkspace.Definitions.First(value => value.StableId == "character_test.fireball.projectile");
        ProjectileAuthoringWorkspaceDefinition newAfter = reassigned.ProjectedWorkspace.Definitions.First(value => value.StableId == "character_test.fireball_burst.projectile");
        Require(reassigned.Status == "Ready" && reassignedRequest.DefinitionStableId == "character_test.fireball_burst.projectile" && reassignedRequest.Detail.ProjectileStableId == reassignedRequest.DefinitionStableId, "M3-C02 direct request projection summary is stale after definition reassignment.");
        Require(!oldAfter.RequestIds.Contains(reassignedRequest.StableId) && oldAfter.RequestIds.Count == oldBefore.RequestIds.Count - 1 && oldAfter.Shared == (oldAfter.RequestIds.Count > 1), "M3-C02 direct request projection did not remove the old definition reference or refresh its shared state.");
        Require(newAfter.RequestIds.Contains(reassignedRequest.StableId) && newAfter.RequestIds.Count == newBefore.RequestIds.Count + 1 && newAfter.Shared == (newAfter.RequestIds.Count > 1), "M3-C02 direct request projection did not add the new definition reference or refresh its shared state.");
    }

    private static void DormantAreaFieldsFallBackWithoutLosingUnknowns()
    {
        string root = CopySample();
        try
        {
            string sourcePath = Path.Combine(root, "config", "projectiles", "character_test.fireball.projectile.json");
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath)).AsObject();
            JsonObject impact = source["definition"]["impact"].AsObject();
            impact["areaRadius"] = -2f;
            impact["areaHitEvent"] = new JsonObject
            {
                ["targetTypes"] = new JsonArray(),
                ["environmentHitPolicy"] = "InvalidDormantPolicy",
                ["stableEventId"] = string.Empty,
                ["maxAcceptedHits"] = 0,
                ["hitSameTargetPolicy"] = "InvalidDormantRepeat",
                ["damage"] = new JsonObject
                {
                    ["damageTypeId"] = string.Empty,
                    ["baseDamage"] = -1,
                    ["impactPower"] = -1,
                    ["launchPower"] = -1,
                    ["tags"] = new JsonArray(string.Empty),
                    ["unknownDamageExtension"] = 83,
                },
                ["onHitBuffs"] = new JsonArray(new JsonObject { ["buffId"] = 0 }),
                ["unknownDormantAreaExtension"] = 71,
            };
            File.WriteAllText(sourcePath, source.ToJsonString());

            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            Require(!workspace.Compile().HasBlockingIssues, "M3-C07 invalid dormant area-only fields should not invalidate a DirectHit source.");
            ProjectileAuthoringMutationResult result = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Enum("impact.policyKind", "AreaPulseOnImpact"));
            Require(result.Status == "Ready", "M3-C07 Direct to Area should repair invalid dormant known fields from direct/default values.");
            JsonElement area = result.Draft.Documents[0].Json.GetProperty("definition").GetProperty("impact").GetProperty("areaHitEvent");
            Require(area.GetProperty("targetTypes")[0].GetString() == "HostileCharacter" && area.GetProperty("environmentHitPolicy").GetString() == "None" && area.GetProperty("maxAcceptedHits").GetInt32() == 1 && area.GetProperty("hitSameTargetPolicy").GetString() == "AllowRepeat", "M3-C07 dormant area hit policies did not inherit valid direct values.");
            Require((area.GetProperty("damage").TryGetProperty("damageTypeLabel", out var dtl) ? dtl.GetString() == "fire" : area.GetProperty("damage").GetProperty("damageTypeId").GetString() == "fire") && area.GetProperty("damage").GetProperty("baseDamage").GetInt32() == 10 && (area.GetProperty("damage").TryGetProperty("tagLabels", out var tl) ? tl[0].GetString() == "area" : area.GetProperty("damage").GetProperty("tags")[0].GetString() == "area") && area.GetProperty("onHitBuffs")[0].GetProperty("buffId").GetInt32() == 601009, "M3-C07 dormant area damage/tags/buffs did not inherit valid direct values.");
            Require(area.GetProperty("unknownDormantAreaExtension").GetInt32() == 71 && area.GetProperty("damage").GetProperty("unknownDamageExtension").GetInt32() == 83, "M3-C07/C13 dormant area fallback lost unknown fields.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void CompoundTypeChangesApplyStableDefaults()
    {
        string root = CopySample();
        try
        {
            string sourcePath = Path.Combine(root, "config", "projectiles", "character_test.fireball.projectile.json");
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath)).AsObject();
            source["definition"]["motion"]["unknownMotionExtension"] = new JsonObject { ["keep"] = 31 };
            source["definition"]["impact"]["unknownImpactExtension"] = new JsonObject { ["keep"] = 47 };
            source["definition"]["impact"]["hitEvent"]["unknownDirectHitExtension"] = "opaque";
            File.WriteAllText(sourcePath, source.ToJsonString());

            using (ProjectileAuthoringWorkspace workspace = LoadWorkspace(root))
            {
                string[] motions =
                {
                    "Linear", "ArcToTarget", "BoomerangReturn", "SwayForward", "HomingToTarget", "BezierToTarget", "BallisticArcToTarget"
                };
                for (int i = 0; i < motions.Length; i++)
                {
                    string kind = motions[i];
                    ProjectileAuthoringMutationResult result = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Enum("motion.kind", kind));
                    Require(result.Status == "Ready", "M3-C04 generic motion switch should be Ready: " + kind + ".");
                    JsonElement motion = result.Draft.Documents[0].Json.GetProperty("definition").GetProperty("motion");
                    ProjectileMotionConfig detail = result.ProjectedWorkspace.Definitions.First(value => value.StableId == "character_test.fireball.projectile").Detail.Motion;
                    Require(motion.GetProperty("kind").GetString() == kind && detail.Kind == kind, "M3-C04 raw/projected motion kind diverged: " + kind + ".");
                    Require(motion.GetProperty("unknownMotionExtension").GetProperty("keep").GetInt32() == 31, "M3-C04/C13 motion switch lost an unknown sibling: " + kind + ".");
                    if (string.Equals(kind, "ArcToTarget", StringComparison.Ordinal))
                        Require(detail.ArcHeight == 1f, "M3-C04 ArcToTarget default arcHeight is unstable.");
                    else if (string.Equals(kind, "SwayForward", StringComparison.Ordinal))
                        Require(detail.LateralAmplitude == .5f && detail.LateralCycles == 1, "M3-C04 SwayForward defaults are unstable.");
                    else if (string.Equals(kind, "HomingToTarget", StringComparison.Ordinal))
                        Require(detail.Homing?.TurnRatePerFrame == 1f, "M3-C04 HomingToTarget default turn rate is unstable.");
                    else if (string.Equals(kind, "BezierToTarget", StringComparison.Ordinal))
                        Require(detail.BezierControlOffsetA.Y == 1f && detail.BezierControlOffsetA.Z == 0f && detail.BezierControlOffsetB.Y == 1f && detail.BezierControlOffsetB.Z == 1f, "M3-C04 BezierToTarget defaults are unstable.");
                    else if (string.Equals(kind, "BallisticArcToTarget", StringComparison.Ordinal))
                        Require(detail.GravityPerFrameSquared == .01f && detail.MaxLaunchAngleDegrees == 45f && detail.MaxLaunchAngleTangent > .99f && detail.MaxLaunchAngleTangent < 1.01f, "M3-C04 BallisticArcToTarget defaults/tangent are unstable.");
                }

                ProjectileAuthoringMutationResult invalidArc = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Enum("motion.kind", "ArcToTarget"), Float("motion.arcHeight", 0f));
                Require(invalidArc.Status == "Blocked" && invalidArc.Diagnostics.Any(value => value.Code == "PROJAUTH028"), "M3-C04 explicit invalid compound motion input must not be replaced by an authoring default.");

                ProjectileAuthoringMutationResult area = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Enum("impact.policyKind", "AreaPulseOnImpact"));
                Require(area.Status == "Ready", "M3-C07 generic DirectHit to AreaPulseOnImpact switch should be Ready.");
                JsonElement areaDefinition = area.Draft.Documents[0].Json.GetProperty("definition");
                JsonElement areaImpact = areaDefinition.GetProperty("impact");
                JsonElement areaHit = areaImpact.GetProperty("areaHitEvent");
                ProjectileImpactConfig areaDetail = area.ProjectedWorkspace.Definitions.First(value => value.StableId == "character_test.fireball.projectile").Detail.Impact;
                Require(areaImpact.GetProperty("policyKind").GetString() == "AreaPulseOnImpact" && areaImpact.GetProperty("areaRadius").GetSingle() == 1f && !areaImpact.TryGetProperty("areaLayerMask", out _) && areaDetail.PolicyKind == "AreaPulseOnImpact" && areaDetail.AreaRadius == 1f && areaDetail.AreaLayerMask == (1 << 8), "M3-C07 AreaPulseOnImpact raw/projected defaults or derived mask diverged.");
                Require(areaHit.GetProperty("targetTypes")[0].GetString() == "HostileCharacter" && (areaHit.GetProperty("damage").TryGetProperty("damageTypeLabel", out var adtl) ? adtl.GetString() == "fire" : areaHit.GetProperty("damage").GetProperty("damageTypeId").GetString() == "fire"), "M3-C07 area hit did not inherit valid direct targets/damage.");
                Require((areaHit.GetProperty("damage").TryGetProperty("tagLabels", out var atl) ? atl.EnumerateArray().Select(value => value.GetString()).SequenceEqual(new[] { "area", "fireball", "magic", "scatter" }) : areaHit.GetProperty("damage").GetProperty("tags").EnumerateArray().Select(value => value.GetString()).SequenceEqual(new[] { "area", "fireball", "magic", "scatter" })) && areaHit.GetProperty("onHitBuffs")[0].GetProperty("buffId").GetInt32() == 601009, "M3-C07 area hit did not inherit direct tags/buffs.");
                Require(areaHit.GetProperty("unknownDirectHitExtension").GetString() == "opaque" && areaImpact.GetProperty("unknownImpactExtension").GetProperty("keep").GetInt32() == 47, "M3-C07/C13 Direct to Area switch lost unknown hit/impact data.");

                ProjectileAuthoringMutationResult invalidArea = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Enum("impact.policyKind", "AreaPulseOnImpact"), Float("impact.areaRadius", 0f));
                Require(invalidArea.Status == "Blocked" && invalidArea.Diagnostics.Any(value => value.Code == "PROJAUTH028"), "M3-C07 explicit invalid areaRadius must not be replaced by an authoring default.");

                JsonObject areaSource = JsonNode.Parse(area.Draft.Documents[0].Json.GetRawText()).AsObject();
                areaSource["definition"]["impact"]["areaHitEvent"]["unknownAreaExtension"] = new JsonObject { ["keep"] = 59 };
                File.WriteAllText(sourcePath, areaSource.ToJsonString());
            }

            using (ProjectileAuthoringWorkspace areaWorkspace = LoadWorkspace(root))
            {
                ProjectileAuthoringCompileResult areaCompile = areaWorkspace.Compile();
                Require(!areaCompile.HasBlockingIssues, "M3-C07 normalized area fixture should compile before switching back to DirectHit: " + string.Join(" | ", areaCompile.Diagnostics.Select(value => value.Code + ":" + value.Message)));
                ProjectileAuthoringMutationResult direct = Plan(areaWorkspace, "EditDefinitionFields", "character_test.fireball.projectile", Enum("impact.policyKind", "DirectHit"));
                Require(direct.Status == "Ready", "M3-C07 generic AreaPulseOnImpact to DirectHit switch should be Ready.");
                JsonElement impact = direct.Draft.Documents[0].Json.GetProperty("definition").GetProperty("impact");
                ProjectileImpactConfig detail = direct.ProjectedWorkspace.Definitions.First(value => value.StableId == "character_test.fireball.projectile").Detail.Impact;
                Require(impact.GetProperty("policyKind").GetString() == "DirectHit" && !impact.GetProperty("triggerOnExpire").GetBoolean() && !impact.TryGetProperty("areaLayerMask", out _) && detail.PolicyKind == "DirectHit" && detail.AreaLayerMask == 0, "M3-C07 Area to Direct raw/projected normalization diverged.");
                Require(impact.GetProperty("unknownImpactExtension").GetProperty("keep").GetInt32() == 47 && impact.GetProperty("areaHitEvent").GetProperty("unknownAreaExtension").GetProperty("keep").GetInt32() == 59, "M3-C07/C13 Area to Direct switch lost unknown impact/area data.");
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private static void FieldEditsParticipateInOrderedSequence()
    {
        string root = CopySample();
        try
        {
            string sourcePath = Path.Combine(root, "config", "projectiles", "character_test.fireball.projectile.json");
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath)).AsObject();
            source["spawnProfile"]["unknownSequenceSibling"] = new JsonObject { ["keep"] = 91 };
            File.WriteAllText(sourcePath, source.ToJsonString());

            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            string firstToken = ProjectileAuthoringWorkspaceProjectionBuilder.Build(workspace, workspace.Compile()).WorkspaceHash;
            var first = new ProjectileAuthoringMutationRequest
            {
                Kind = "EditRequestFields",
                SourceStableId = "character_test.fireball.projectile",
                WorkspaceToken = firstToken,
                FieldEdits = new List<ProjectileAuthoringFieldEdit> { Float("aim.maxRange", 41f) }
            };
            ProjectileAuthoringMutationResult firstPlan = ProjectileAuthoringMutationPlanner.Plan(workspace, first, Options);
            Require(firstPlan.Status == "Ready", "M3-C14 first token-guarded field edit should be ready.");
            var second = new ProjectileAuthoringMutationRequest
            {
                Kind = "EditRequestFields",
                SourceStableId = "character_test.fireball.projectile",
                WorkspaceToken = firstPlan.ProjectedWorkspace.WorkspaceHash,
                FieldEdits = new List<ProjectileAuthoringFieldEdit> { Vector("origin.fallbackRootOffset", 4f, 5f, 6f) }
            };
            ProjectileAuthoringMutationResult sequence = ProjectileAuthoringMutationSequencePlanner.Plan(workspace, new ProjectileAuthoringMutationSequenceRequest
            {
                Commands = new List<ProjectileAuthoringMutationRequest> { first, second }
            }, Options);

            Require(sequence.Status == "Ready" && sequence.Draft.Documents.Count == 1, "M3-C14 two field commands should collapse into one composite source draft.");
            ProjectileSpawnProfileConfig detail = sequence.ProjectedWorkspace.Requests.First(value => value.StableId == "character_test.fireball.projectile").Detail;
            Require(detail.Aim.MaxRange == 41f && detail.Origin.FallbackRootOffset.Y == 5f, "M3-C14 ordered field edits are missing from the final projected detail.");
            JsonElement spawn = sequence.Draft.Documents[0].Json.GetProperty("spawnProfile");
            Require(spawn.GetProperty("aim").GetProperty("maxRange").GetSingle() == 41f && spawn.GetProperty("origin").GetProperty("fallbackRootOffset").GetProperty("z").GetSingle() == 6f, "M3-C14 ordered field edits are missing from the final raw draft.");
            Require(spawn.GetProperty("unknownSequenceSibling").GetProperty("keep").GetInt32() == 91, "M3-C13/C14 sequence replay lost an unknown request sibling.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void RequestCompilerDefaultsRemainEditable()
    {
        string root = CopySample();
        try
        {
            string sourcePath = Path.Combine(root, "config", "projectiles", "character_test.fireball.projectile.json");
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath)).AsObject();
            source["spawnProfile"]["origin"].AsObject().Remove("kind");
            source["spawnProfile"]["aim"].AsObject().Remove("fallbackKind");
            File.WriteAllText(sourcePath, source.ToJsonString());

            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            ProjectileAuthoringMutationResult result = Plan(workspace, "EditRequestFields", "character_test.fireball.projectile", Float("aim.maxRange", 29f));
            Require(result.Status == "Ready", "M3-C02/C03 compiler-default origin.kind and aim.fallbackKind should not block an unrelated request field edit.");
            JsonElement spawn = result.Draft.Documents[0].Json.GetProperty("spawnProfile");
            Require(!spawn.GetProperty("origin").TryGetProperty("kind", out _) && !spawn.GetProperty("aim").TryGetProperty("fallbackKind", out _), "M3-C13 request validation materialized untouched compiler-default fields.");
            ProjectileSpawnProfileConfig detail = result.ProjectedWorkspace.Requests.First(value => value.StableId == "character_test.fireball.projectile").Detail;
            Require(detail.Origin.Kind == "Socket" && detail.Aim.FallbackKind == "CasterForwardRange", "M3-C02/C03 typed projection did not expose compiler-default request values.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void FieldEditDraftAppliesAndRollsBackAtomically()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-projauth-m3-apply-" + Guid.NewGuid().ToString("N"));
        string package = Path.Combine(root, "package");
        string failurePackage = Path.Combine(root, "failure-package");
        string generated = Path.Combine(root, "generated");
        string failureGenerated = Path.Combine(root, "failure-generated");
        string draftPath = Path.Combine(root, "field-edit-draft.json");
        try
        {
            CopyDirectory(SamplePath(), package);
            string relative = "config/projectiles/character_test.fireball.projectile.json";
            string sourcePath = Path.Combine(package, relative.Replace('/', Path.DirectorySeparatorChar));
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath)).AsObject();
            source["unknownApplySibling"] = new JsonObject { ["keep"] = 73 };
            File.WriteAllText(sourcePath, source.ToJsonString());
            CopyDirectory(package, failurePackage);

            using (ProjectileAuthoringWorkspace workspace = LoadWorkspace(package))
            {
                ProjectileAuthoringMutationResult edit = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Float("motion.speedPerFrame", .71f));
                Require(edit.Status == "Ready" && edit.Draft.Documents.Count == 1, "M3-C19 field edit draft should be ready for M1 Apply.");
                File.WriteAllText(draftPath, JsonSerializer.Serialize(edit.Draft, Options));
            }

            int applied = MxFramework.Authoring.Cli.Program.Main(new[] { "projectile", "apply", "--package", package, "--draft", draftPath, "--out", generated, "--vfx-definitions", GlobalVfxDefinitionsPath() });
            Require(applied == 0, "M3-C19 field edit draft failed M1 Apply.");
            using JsonDocument appliedSource = JsonDocument.Parse(File.ReadAllBytes(sourcePath));
            Require(appliedSource.RootElement.GetProperty("definition").GetProperty("motion").GetProperty("speedPerFrame").GetSingle() == .71f && appliedSource.RootElement.GetProperty("unknownApplySibling").GetProperty("keep").GetInt32() == 73, "M3-C19 Apply changed data outside the requested field or lost an unknown sibling.");
            Require(Directory.GetFiles(generated).Length == 7, "M3-C19 Apply did not atomically publish the seven projectile artifacts.");

            string failureSourcePath = Path.Combine(failurePackage, relative.Replace('/', Path.DirectorySeparatorChar));
            byte[] failureSourceBefore = File.ReadAllBytes(failureSourcePath);
            int failed = MxFramework.Authoring.Cli.ProjectilePackageCommands.ApplyForTests(
                new[] { "projectile", "apply", "--package", failurePackage, "--draft", draftPath, "--out", failureGenerated },
                Options,
                new FailAfterFourCommits());
            Require(failed != 0, "M3-C19 injected field edit Apply should report failure.");
            Require(File.ReadAllBytes(failureSourcePath).SequenceEqual(failureSourceBefore), "M3-C19 failed Apply left a partial source field edit on disk.");
            Require(!Directory.Exists(failureGenerated) || Directory.GetFiles(failureGenerated).Length == 0, "M3-C19 failed Apply left partial generated artifacts on disk.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void DiagnosticsCover025Through029AndSharedConfirmation()
    {
        using ProjectileAuthoringWorkspace workspace = LoadWorkspace(SamplePath());
        Require(Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Int("definitionId", 2)).Diagnostics.Any(value => value.Code == "PROJAUTH025"), "M3-C16 PROJAUTH025 is untested or missing.");
        Require(Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", String("motion.speedPerFrame", "bad")).Diagnostics.Any(value => value.Code == "PROJAUTH026"), "M3-C16 PROJAUTH026 is untested or missing.");
        Require(Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Float("motion.speedPerFrame", -1)).Diagnostics.Any(value => value.Code == "PROJAUTH027"), "M3-C16 PROJAUTH027 is untested or missing.");
        Require(Plan(workspace, "EditRequestFields", "character_test.fireball.projectile", Enum("aim.kind", "LockedTargetCenter"), Enum("aim.fallbackKind", "LockedTargetCenter")).Diagnostics.Any(value => value.Code == "PROJAUTH028"), "M3-C16 PROJAUTH028 is untested or missing.");

        var previewRequest = new ProjectileAuthoringMutationRequest { Kind = "EditDefinitionFields", SourceStableId = "character_test.meteor_barrage.projectile", PlanOnly = true, FieldEdits = new List<ProjectileAuthoringFieldEdit> { Float("motion.speedPerFrame", .5f) } };
        ProjectileAuthoringMutationResult preview = ProjectileAuthoringMutationPlanner.Plan(workspace, previewRequest, Options);
        Require(preview.Status == "Ready" && preview.Impact.Blocking && preview.Impact.RequestStableIds.Count == 6 && preview.Impact.FieldPaths.SequenceEqual(new[] { "motion.speedPerFrame" }), "M3-C17 shared definition impact is incomplete.");
        var confirmed = new ProjectileAuthoringMutationRequest { Kind = "EditDefinitionFields", SourceStableId = "character_test.meteor_barrage.projectile", Confirmed = true, ConfirmationToken = preview.Impact.ConfirmationToken, WorkspaceToken = preview.Impact.WorkspaceToken, FieldEdits = previewRequest.FieldEdits };
        ProjectileAuthoringMutationResult accepted = ProjectileAuthoringMutationPlanner.Plan(workspace, confirmed, Options);
        Require(accepted.Status == "Ready" && accepted.Draft.Documents.Count == 1, "M3-C18 current shared confirmation should be accepted.");
        confirmed.WorkspaceToken = "sha256:stale";
        ProjectileAuthoringMutationResult stale = ProjectileAuthoringMutationPlanner.Plan(workspace, confirmed, Options);
        Require(stale.Status == "Blocked" && stale.Draft.Documents.Count == 0 && stale.Diagnostics.Any(value => value.Code == "PROJAUTH029"), "M3-C16/C18 stale workspace confirmation should fail with PROJAUTH029.");
    }

    private static void CompilerCompatiblePhysicalAndInheritedAreaCasesRemainEditable()
    {
        string root = CopySample();
        try
        {
            string path = Path.Combine(root, "config", "projectiles", "character_test.fireball.projectile.json");
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(path)).AsObject();
            JsonObject definition = source["definition"].AsObject();
            definition["physicalEventsEnabled"] = true;
            JsonObject impact = definition["impact"].AsObject();
            impact["policyKind"] = "AreaPulseOnImpact";
            impact["areaRadius"] = 2f;
            impact["areaHitEvent"] = new JsonObject
            {
                ["targetTypes"] = new JsonArray("None"),
                ["environmentHitPolicy"] = "None",
                ["stableEventId"] = "area.inherited",
                ["maxAcceptedHits"] = 1,
                ["hitSameTargetPolicy"] = "AllowRepeat"
            };
            impact["hitEvent"]["targetTypes"] = new JsonArray("None");
            File.WriteAllText(path, source.ToJsonString());
            using ProjectileAuthoringWorkspace workspace = LoadWorkspace(root);
            ProjectileAuthoringMutationResult result = Plan(workspace, "EditDefinitionFields", "character_test.fireball.projectile", Bool("collision.stopOnBlockedImpact", false));
            Require(result.Status == "Ready", "M3-C07 compiler-valid inherited area damage and physical no-direct-sweep must remain editable.");
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class FailAfterFourCommits : IProjectileAuthoringTransactionFaultInjector
    {
        public void BeforeCommit(int committedItemCount, string receiptPath)
        {
            if (committedItemCount == 4)
                throw new IOException("Injected M3 field edit Apply failure.");
        }

        public void BeforeRollback(string receiptPath)
        {
        }
    }

    private static ProjectileAuthoringMutationResult Plan(ProjectileAuthoringWorkspace workspace, string kind, string id, params ProjectileAuthoringFieldEdit[] edits) => ProjectileAuthoringMutationPlanner.Plan(workspace, new ProjectileAuthoringMutationRequest { Kind = kind, SourceStableId = id, FieldEdits = edits.ToList() }, Options);
    private static ProjectileAuthoringFieldEdit Bool(string path, bool value) => new ProjectileAuthoringFieldEdit { FieldPath = path, ValueKind = "Bool", BoolValue = value };
    private static ProjectileAuthoringFieldEdit Int(string path, int value) => new ProjectileAuthoringFieldEdit { FieldPath = path, ValueKind = "Int", IntValue = value };
    private static ProjectileAuthoringFieldEdit Float(string path, float value) => new ProjectileAuthoringFieldEdit { FieldPath = path, ValueKind = "Float", FloatValue = value };
    private static ProjectileAuthoringFieldEdit String(string path, string value, string kind = "String") => new ProjectileAuthoringFieldEdit { FieldPath = path, ValueKind = kind, StringValue = value };
    private static ProjectileAuthoringFieldEdit Enum(string path, string value) => String(path, value, "Enum");
    private static ProjectileAuthoringFieldEdit Strings(string path, params string[] values) => new ProjectileAuthoringFieldEdit { FieldPath = path, ValueKind = "StringList", StringValues = values.ToList() };
    private static ProjectileAuthoringFieldEdit Ints(string path, params int[] values) => new ProjectileAuthoringFieldEdit { FieldPath = path, ValueKind = "IntList", IntValues = values.ToList() };
    private static ProjectileAuthoringFieldEdit Vector(string path, float x, float y, float z) => new ProjectileAuthoringFieldEdit { FieldPath = path, ValueKind = "Vector", VectorValue = new ProjectileVector3Config { X = x, Y = y, Z = z } };
    private static string CopySample() { string target = Path.Combine(Path.GetTempPath(), "mx-projauth-m3-" + Guid.NewGuid().ToString("N")); CopyDirectory(SamplePath(), target); return target; }
    private static void CopyDirectory(string source, string target) { Directory.CreateDirectory(target); foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file))); foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory))); }
    private static JsonSerializerOptions CreateOptions() { var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }; options.Converters.Add(new JsonStringEnumConverter()); return options; }
    private static ProjectileAuthoringWorkspace LoadWorkspace(string packagePath) =>
        ProjectileAuthoringWorkspace.Load(packagePath, null, GlobalVfxDefinitionsPath(), Options);

    private static string GlobalVfxDefinitionsPath() => Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(),
        "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json"));

    private static string SamplePath() => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Tools/MxFramework.Authoring/samples/ability-character-test"));
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
