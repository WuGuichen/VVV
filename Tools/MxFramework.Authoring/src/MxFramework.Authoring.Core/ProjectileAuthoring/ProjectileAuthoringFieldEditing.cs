using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MxFramework.Authoring
{
    /// <summary>One allowlisted, strongly typed projectile authoring field replacement.</summary>
    /// <remarks>LowFreqAlloc: serialized and evaluated only for an interactive authoring commit.</remarks>
    public sealed class ProjectileAuthoringFieldEdit
    {
        public string FieldPath { get; set; } = string.Empty;
        public string ValueKind { get; set; } = string.Empty;
        public bool BoolValue { get; set; }
        public int IntValue { get; set; }
        public float FloatValue { get; set; }
        public string StringValue { get; set; } = string.Empty;
        public List<string> StringValues { get; set; } = new List<string>();
        public List<int> IntValues { get; set; } = new List<int>();
        public ProjectileVector3Config VectorValue { get; set; } = new ProjectileVector3Config();
        public JsonElement JsonValue { get; set; }
    }

    /// <summary>Plans lossless field-level projectile definition and request mutations.</summary>
    /// <remarks>LowFreqAlloc: clones JSON nodes and builds diagnostics only during editor interaction.</remarks>
    public static class ProjectileAuthoringFieldEditor
    {
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly HashSet<string> MotionKinds = Set("Linear", "ArcToTarget", "BoomerangReturn", "SwayForward", "HomingToTarget", "BezierToTarget", "BallisticArcToTarget");
        private static readonly HashSet<string> SpeedKinds = Set("Constant", "EaseInQuadratic", "EaseOutQuadratic", "EaseInOutSmoothstep");
        private static readonly HashSet<string> ImpactKinds = Set("DirectHit", "AreaPulseOnImpact");
        private static readonly HashSet<string> CollisionRepeatKinds = Set("AllowRepeat", "OncePerProjectile");
        private static readonly HashSet<string> HitRepeatKinds = Set("AllowRepeat", "OncePerEvent");
        private static readonly HashSet<string> EnvironmentKinds = Set("None", "BlockedPresentation");
        private static readonly HashSet<string> OriginKinds = Set("Socket");
        private static readonly HashSet<string> AimKinds = Set("CasterForwardRange", "LockedTargetCenter", "LockedTargetOrCasterForwardRange");
        private static readonly HashSet<string> TargetKinds = Set("None", "HostileCharacter", "FriendlyCharacter", "Self", "ThirdPartyCharacter", "NeutralCharacter", "DamageableObject", "WorldWall", "WorldGround", "WorldStatic");
        private const float DefaultArcHeight = 1f;
        private const float DefaultSwayAmplitude = .5f;
        private const int DefaultSwayCycles = 1;
        private const float DefaultBallisticGravity = .01f;
        private const float DefaultBallisticAngleDegrees = 45f;
        private const float DefaultHomingTurnRate = 1f;
        private const float DefaultAreaRadius = 1f;

        /// <summary>Plans one atomic, lossless batch of definition field replacements.</summary>
        /// <remarks>LowFreqAlloc: clones and validates source JSON for an authoring commit.</remarks>
        public static void EditDefinition(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result)
        {
            string stableId = request.SourceStableId ?? string.Empty;
            ProjectileAuthoringSourceDocument[] matches = workspace.SourceDocuments.Where(value => value.Projection?.Definition != null && string.Equals(value.Projection.Definition.StableId, stableId, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                Error(result, "PROJAUTH028", stableId, string.Empty, "The definition source must exist exactly once before its fields can be edited.");
                return;
            }

            PrepareImpact(workspace, request, result, stableId);
            JsonObject root = ParseRoot(matches[0], result, stableId);
            if (root == null || root["definition"] is not JsonObject definition)
            {
                Error(result, "PROJAUTH028", stableId, "definition", "The definition object is required.");
                return;
            }

            ApplyEdits(definition, request.FieldEdits, true, result, matches[0], stableId);
            NormalizeDefinition(definition, request.FieldEdits);
            ValidateDefinition(definition, workspace, result, matches[0], stableId);
            if (result.Diagnostics.Any(value => string.Equals(value.Severity, "Error", StringComparison.OrdinalIgnoreCase)))
                return;

            if (request.PlanOnly)
                return;
            if (result.Impact.RequestStableIds.Count > 1
                && (!request.Confirmed || !string.Equals(request.ConfirmationToken, result.Impact.ConfirmationToken, StringComparison.Ordinal)))
            {
                Error(result, "PROJAUTH029", stableId, string.Empty, "The shared definition confirmation is missing or stale for the current workspace token.");
                return;
            }

            AddDraft(result, matches[0], root);
            BuildProjected(workspace, result, matches[0], root, stableId, true);
            result.Impact.Blocking = false;
            result.Impact.BlockingReasons.RemoveAll(value => string.Equals(value, "shared-definition-confirmation-required", StringComparison.Ordinal));
        }

        /// <summary>Plans one atomic, lossless batch of request field replacements.</summary>
        /// <remarks>LowFreqAlloc: clones and validates source JSON for an authoring commit.</remarks>
        public static void EditRequest(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result)
        {
            string stableId = request.SourceStableId ?? string.Empty;
            if (!workspace.ReferenceIndex.RequestsByStableId.TryGetValue(stableId, out ProjectileAuthoringSourceDocument source))
            {
                Error(result, "PROJAUTH028", stableId, string.Empty, "The projectile request was not found.");
                return;
            }

            string workspaceToken = ComputeWorkspaceToken(workspace);
            result.Impact.StableId = stableId;
            result.Impact.WorkspaceToken = workspaceToken;
            AddFieldPaths(result, request.FieldEdits);
            if (!string.IsNullOrEmpty(request.WorkspaceToken) && !string.Equals(request.WorkspaceToken, workspaceToken, StringComparison.Ordinal))
            {
                Error(result, "PROJAUTH029", stableId, string.Empty, "The workspace token is stale.");
                return;
            }

            JsonObject root = ParseRoot(source, result, stableId);
            if (root == null || root["spawnProfile"] is not JsonObject spawn)
            {
                Error(result, "PROJAUTH028", stableId, "spawnProfile", "The request spawnProfile object is required.");
                return;
            }

            ApplyEdits(spawn, request.FieldEdits, false, result, source, stableId);
            NormalizeRequest(spawn, request.FieldEdits);
            ValidateRequest(spawn, workspace, result, source, stableId);
            if (result.Diagnostics.Any(value => string.Equals(value.Severity, "Error", StringComparison.OrdinalIgnoreCase)) || request.PlanOnly)
                return;

            AddDraft(result, source, root);
            BuildProjected(workspace, result, source, root, stableId, false);
        }

        private static void PrepareImpact(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result, string stableId)
        {
            result.Impact.StableId = stableId;
            if (workspace.ReferenceIndex.RequestIdsByDefinitionStableId.TryGetValue(stableId, out IReadOnlyList<string> requests))
                result.Impact.RequestStableIds.AddRange(requests.OrderBy(value => value, StringComparer.Ordinal));
            AddFieldPaths(result, request.FieldEdits);
            result.Impact.WorkspaceToken = ComputeWorkspaceToken(workspace);
            result.Impact.ConfirmationToken = ProjectileAuthoringHashes.ComputeSha256(Encoding.UTF8.GetBytes(
                result.Impact.WorkspaceToken + "\n" + stableId + "\n" + string.Join("\n", result.Impact.RequestStableIds) + "\n" + string.Join("\n", result.Impact.FieldPaths)));
            result.Impact.Summary = result.Impact.RequestStableIds.Count + " request(s) are affected by fields: " + string.Join(", ", result.Impact.FieldPaths);
            if (!string.IsNullOrEmpty(request.WorkspaceToken) && !string.Equals(request.WorkspaceToken, result.Impact.WorkspaceToken, StringComparison.Ordinal))
                Error(result, "PROJAUTH029", stableId, string.Empty, "The workspace token is stale.");
            if (result.Impact.RequestStableIds.Count > 1
                && (!request.Confirmed || !string.Equals(request.ConfirmationToken, result.Impact.ConfirmationToken, StringComparison.Ordinal)))
            {
                result.Impact.Blocking = true;
                result.Impact.BlockingReasons.Add("shared-definition-confirmation-required");
            }
        }

        private static void AddFieldPaths(ProjectileAuthoringMutationResult result, List<ProjectileAuthoringFieldEdit> edits)
        {
            for (int i = 0; edits != null && i < edits.Count; i++)
            {
                string path = (edits[i]?.FieldPath ?? string.Empty).Trim();
                if (path.Length > 0 && !result.Impact.FieldPaths.Contains(path, StringComparer.Ordinal))
                    result.Impact.FieldPaths.Add(path);
            }
        }

        private static void ApplyEdits(JsonObject target, List<ProjectileAuthoringFieldEdit> edits, bool definition, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string stableId)
        {
            if (edits == null || edits.Count == 0)
            {
                Error(result, "PROJAUTH028", stableId, string.Empty, "At least one field edit is required.");
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < edits.Count; i++)
            {
                ProjectileAuthoringFieldEdit edit = edits[i];
                string path = (edit?.FieldPath ?? string.Empty).Trim();
                if (path.Length == 0 || !seen.Add(path))
                {
                    Error(result, "PROJAUTH028", stableId, path, "Field paths must be non-empty and unique inside one atomic edit batch.", source);
                    continue;
                }
                bool known = definition
                    ? ApplyDefinitionField(target, edit, result, source, stableId)
                    : ApplyRequestField(target, edit, result, source, stableId);
                if (!known)
                    Error(result, "PROJAUTH025", stableId, path, "The field path is unknown or read-only in this inspector.", source);
            }
        }

        private static bool ApplyDefinitionField(JsonObject target, ProjectileAuthoringFieldEdit edit, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        {
            string path = edit.FieldPath;
            switch (path)
            {
                case "physicalEventsEnabled": return PutBool(target, edit, path, result, source, id);
                case "motion.kind": return PutEnum(target, edit, path, MotionKinds, result, source, id);
                case "motion.speedPerFrame": case "motion.arcHeight": case "motion.gravityPerFrameSquared": case "motion.maxLaunchAngleDegrees": case "motion.lateralAmplitude": case "motion.homing.turnRatePerFrame": case "collision.radius": case "impact.areaRadius": return PutFloat(target, edit, path, result, source, id);
                case "motion.lifetimeFrames": case "motion.lateralCycles": case "collision.maxAcceptedImpacts": case "impact.hitEvent.maxAcceptedHits": case "impact.areaHitEvent.maxAcceptedHits": case "impact.hitEvent.damage.baseDamage": case "impact.hitEvent.damage.impactPower": case "impact.hitEvent.damage.launchPower": case "impact.areaHitEvent.damage.baseDamage": case "impact.areaHitEvent.damage.impactPower": case "impact.areaHitEvent.damage.launchPower": return PutInt(target, edit, path, result, source, id);
                case "motion.bezierControlOffsetA": case "motion.bezierControlOffsetB": return PutVector(target, edit, path, result, source, id);
                case "motion.speedProfile.kind": return PutEnum(target, edit, path, SpeedKinds, result, source, id);
                case "collision.stopOnFirstAcceptedImpact": case "collision.stopOnBlockedImpact": case "impact.triggerOnExpire": return PutBool(target, edit, path, result, source, id);
                case "collision.hitSameTargetPolicy": return PutEnum(target, edit, path, CollisionRepeatKinds, result, source, id);
                case "impact.policyKind": return PutEnum(target, edit, path, ImpactKinds, result, source, id);
                case "impact.effectTimelineId": return PutNullableString(target, edit, path, result, source, id);
                case "impact.hitEvent.environmentHitPolicy": case "impact.areaHitEvent.environmentHitPolicy": return PutEnum(target, edit, path, EnvironmentKinds, result, source, id);
                case "impact.hitEvent.hitSameTargetPolicy": case "impact.areaHitEvent.hitSameTargetPolicy": return PutEnum(target, edit, path, HitRepeatKinds, result, source, id);
                case "impact.hitEvent.stableEventId": case "impact.areaHitEvent.stableEventId": case "impact.hitEvent.damage.damageTypeId": case "impact.hitEvent.damage.analysisCategory": case "impact.areaHitEvent.damage.damageTypeId": case "impact.areaHitEvent.damage.analysisCategory": return PutString(target, edit, path, result, source, id);
                case "impact.hitEvent.targetTypes": case "impact.areaHitEvent.targetTypes": return PutTargetList(target, edit, path, result, source, id);
                case "impact.hitEvent.damage.tags": case "impact.areaHitEvent.damage.tags": return PutStringList(target, edit, path, result, source, id);
                case "impact.hitEvent.onHitBuffs": case "impact.areaHitEvent.onHitBuffs": return PutBuffList(target, edit, path, result, source, id);
                case "impact.hitEvent.damage": case "impact.areaHitEvent.damage": return PutNullableObject(target, edit, path, result, source, id);
                default: return false;
            }
        }

        private static bool ApplyRequestField(JsonObject target, ProjectileAuthoringFieldEdit edit, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        {
            string path = edit.FieldPath;
            switch (path)
            {
                case "projectileStableId": case "origin.socketId": return PutString(target, edit, path, result, source, id);
                case "origin.kind": return PutEnum(target, edit, path, OriginKinds, result, source, id);
                case "origin.localOffset": case "origin.fallbackRootOffset": return PutVector(target, edit, path, result, source, id);
                case "aim.kind": case "aim.fallbackKind": return PutEnum(target, edit, path, AimKinds, result, source, id);
                case "aim.targetHeightOffset": case "aim.maxRange": return PutFloat(target, edit, path, result, source, id);
                default: return false;
            }
        }

        private static bool PutBool(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (!Kind(edit, "Bool")) return Type(result, source, id, path, "Bool"); Put(root, path, JsonValue.Create(edit.BoolValue)); return true; }
        private static bool PutInt(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (!Kind(edit, "Int")) return Type(result, source, id, path, "Int"); Put(root, path, JsonValue.Create(edit.IntValue)); return true; }
        private static bool PutFloat(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (!Kind(edit, "Float")) return Type(result, source, id, path, "Float"); if (float.IsNaN(edit.FloatValue) || float.IsInfinity(edit.FloatValue)) { Range(result, source, id, path, "A finite number is required."); return true; } Put(root, path, JsonValue.Create(edit.FloatValue)); return true; }
        private static bool PutString(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (!Kind(edit, "String")) return Type(result, source, id, path, "String"); Put(root, path, JsonValue.Create(edit.StringValue ?? string.Empty)); return true; }
        private static bool PutNullableString(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (Kind(edit, "Null")) { Put(root, path, JsonValue.Create(string.Empty)); return true; } return PutString(root, edit, path, result, source, id); }
        private static bool PutEnum(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, HashSet<string> values, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (!Kind(edit, "Enum") && !Kind(edit, "String")) return Type(result, source, id, path, "Enum"); string value = edit.StringValue ?? string.Empty; if (!values.Contains(value)) { Range(result, source, id, path, "Unsupported enum value: " + value + "."); return true; } Put(root, path, JsonValue.Create(value)); return true; }
        private static bool PutVector(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (!Kind(edit, "Vector")) return Type(result, source, id, path, "Vector"); ProjectileVector3Config value = edit.VectorValue; if (value == null || !Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) { Range(result, source, id, path, "A finite three-component vector is required."); return true; } Put(root, path, new JsonObject { ["x"] = value.X, ["y"] = value.Y, ["z"] = value.Z }); return true; }
        private static bool PutStringList(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (!Kind(edit, "StringList")) return Type(result, source, id, path, "StringList"); JsonArray array = new JsonArray(); for (int i = 0; edit.StringValues != null && i < edit.StringValues.Count; i++) { if (string.IsNullOrWhiteSpace(edit.StringValues[i])) { Range(result, source, id, path, "List entries cannot be empty."); continue; } array.Add(edit.StringValues[i]); } Put(root, path, array); return true; }
        private static bool PutTargetList(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (!Kind(edit, "StringList")) return Type(result, source, id, path, "StringList"); JsonArray array = new JsonArray(); var seen = new HashSet<string>(StringComparer.Ordinal); for (int i = 0; edit.StringValues != null && i < edit.StringValues.Count; i++) { string value = edit.StringValues[i] ?? string.Empty; if (!TargetKinds.Contains(value)) { Range(result, source, id, path, "Unsupported target type: " + value + "."); continue; } if (string.Equals(value, "NeutralCharacter", StringComparison.Ordinal)) value = "ThirdPartyCharacter"; if (seen.Add(value)) array.Add(value); } Put(root, path, array); return true; }
        private static bool PutBuffList(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (!Kind(edit, "IntList")) return Type(result, source, id, path, "IntList"); JsonArray array = new JsonArray(); var seen = new HashSet<int>(); for (int i = 0; edit.IntValues != null && i < edit.IntValues.Count; i++) { int value = edit.IntValues[i]; if (value <= 0 || !seen.Add(value)) { Range(result, source, id, path, "Buff IDs must be positive and unique."); continue; } array.Add(new JsonObject { ["buffId"] = value }); } Put(root, path, array); return true; }
        private static bool PutNullableObject(JsonObject root, ProjectileAuthoringFieldEdit edit, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        { if (Kind(edit, "Null")) { Put(root, path, null); return true; } if (!Kind(edit, "Object") || edit.JsonValue.ValueKind != JsonValueKind.Object) return Type(result, source, id, path, "Object or Null"); Put(root, path, JsonNode.Parse(edit.JsonValue.GetRawText())); return true; }

        private static void NormalizeDefinition(JsonObject definition, List<ProjectileAuthoringFieldEdit> edits)
        {
            bool motionKind = Has(edits, "motion.kind");
            bool angle = Has(edits, "motion.maxLaunchAngleDegrees");
            string kind = Text(definition, "motion.kind");
            if (motionKind)
            {
                if (string.Equals(kind, "HomingToTarget", StringComparison.Ordinal))
                {
                    JsonObject homing = EnsureObject(definition, "motion.homing");
                    float turnRate = Number(definition, "motion.homing.turnRatePerFrame");
                    if (!Has(edits, "motion.homing.turnRatePerFrame") && (turnRate <= 0f || turnRate > 1f))
                        homing["turnRatePerFrame"] = DefaultHomingTurnRate;
                }
                else Put(definition, "motion.homing", null);

                if (string.Equals(kind, "ArcToTarget", StringComparison.Ordinal)
                    && !Has(edits, "motion.arcHeight")
                    && Number(definition, "motion.arcHeight") <= 0f)
                {
                    Put(definition, "motion.arcHeight", JsonValue.Create(DefaultArcHeight));
                }

                if (string.Equals(kind, "SwayForward", StringComparison.Ordinal))
                {
                    if (!Has(edits, "motion.lateralAmplitude") && Number(definition, "motion.lateralAmplitude") <= 0f)
                        Put(definition, "motion.lateralAmplitude", JsonValue.Create(DefaultSwayAmplitude));
                    int cycles = Integer(definition, "motion.lateralCycles");
                    if (!Has(edits, "motion.lateralCycles") && (cycles <= 0 || cycles > 1000))
                        Put(definition, "motion.lateralCycles", JsonValue.Create(DefaultSwayCycles));
                }

                if (string.Equals(kind, "BezierToTarget", StringComparison.Ordinal)
                    && !Has(edits, "motion.bezierControlOffsetA")
                    && !Has(edits, "motion.bezierControlOffsetB")
                    && ZeroVector(definition, "motion.bezierControlOffsetA")
                    && ZeroVector(definition, "motion.bezierControlOffsetB"))
                {
                    Put(definition, "motion.bezierControlOffsetA", VectorNode(0f, 1f, 0f));
                    Put(definition, "motion.bezierControlOffsetB", VectorNode(0f, 1f, 1f));
                }

                if (string.Equals(kind, "BallisticArcToTarget", StringComparison.Ordinal))
                {
                    if (!Has(edits, "motion.gravityPerFrameSquared") && Number(definition, "motion.gravityPerFrameSquared") <= 0f)
                        Put(definition, "motion.gravityPerFrameSquared", JsonValue.Create(DefaultBallisticGravity));
                    float degrees = Number(definition, "motion.maxLaunchAngleDegrees");
                    if (!Has(edits, "motion.maxLaunchAngleDegrees") && (degrees <= 0f || degrees >= 90f))
                        Put(definition, "motion.maxLaunchAngleDegrees", JsonValue.Create(DefaultBallisticAngleDegrees));
                }
            }
            if (motionKind || angle)
            {
                if (string.Equals(kind, "BallisticArcToTarget", StringComparison.Ordinal))
                {
                    float degrees = Number(definition, "motion.maxLaunchAngleDegrees");
                    Put(definition, "motion.maxLaunchAngleTangent", degrees <= 0f ? JsonValue.Create(0f) : JsonValue.Create((float)Math.Tan(degrees * Math.PI / 180.0)));
                }
                else
                {
                    Put(definition, "motion.gravityPerFrameSquared", JsonValue.Create(0f));
                    Put(definition, "motion.maxLaunchAngleDegrees", JsonValue.Create(0f));
                    Put(definition, "motion.maxLaunchAngleTangent", JsonValue.Create(0f));
                }
            }
            if (Has(edits, "impact.policyKind") && string.Equals(Text(definition, "impact.policyKind"), "AreaPulseOnImpact", StringComparison.Ordinal))
            {
                if (!Has(edits, "impact.areaRadius") && Number(definition, "impact.areaRadius") <= 0f)
                    Put(definition, "impact.areaRadius", JsonValue.Create(DefaultAreaRadius));
                NormalizeAreaHitEvent(definition, edits);
            }
            else if (Has(edits, "impact.policyKind") && string.Equals(Text(definition, "impact.policyKind"), "DirectHit", StringComparison.Ordinal))
            {
                Put(definition, "impact.triggerOnExpire", JsonValue.Create(false));
            }
        }

        private static void NormalizeAreaHitEvent(JsonObject definition, List<ProjectileAuthoringFieldEdit> edits)
        {
            JsonObject direct = At(definition, "impact.hitEvent") as JsonObject;
            JsonObject area = At(definition, "impact.areaHitEvent") as JsonObject;
            if (area == null)
            {
                area = direct?.DeepClone() as JsonObject ?? new JsonObject();
                Put(definition, "impact.areaHitEvent", area);
            }

            if (!HasExplicitEdit(edits, "impact.areaHitEvent.targetTypes")
                && !ValidAreaTargets(GetStrings(definition, "impact.areaHitEvent.targetTypes")))
            {
                JsonNode targets = ValidAreaTargets(GetStrings(definition, "impact.hitEvent.targetTypes"))
                    ? At(definition, "impact.hitEvent.targetTypes")?.DeepClone()
                    : new JsonArray("HostileCharacter");
                Put(definition, "impact.areaHitEvent.targetTypes", targets);
            }

            if (!HasExplicitEdit(edits, "impact.areaHitEvent.environmentHitPolicy")
                && !EnvironmentKinds.Contains(Text(definition, "impact.areaHitEvent.environmentHitPolicy")))
            {
                string inherited = Text(definition, "impact.hitEvent.environmentHitPolicy");
                Put(definition, "impact.areaHitEvent.environmentHitPolicy", JsonValue.Create(EnvironmentKinds.Contains(inherited) ? inherited : "None"));
            }

            if (!HasExplicitEdit(edits, "impact.areaHitEvent.stableEventId")
                && string.IsNullOrWhiteSpace(Text(definition, "impact.areaHitEvent.stableEventId")))
            {
                string inherited = Text(definition, "impact.hitEvent.stableEventId");
                Put(definition, "impact.areaHitEvent.stableEventId", JsonValue.Create(string.IsNullOrWhiteSpace(inherited) ? "projectile.area" : inherited));
            }

            if (!HasExplicitEdit(edits, "impact.areaHitEvent.maxAcceptedHits")
                && Integer(definition, "impact.areaHitEvent.maxAcceptedHits") <= 0)
            {
                int inherited = Integer(definition, "impact.hitEvent.maxAcceptedHits");
                Put(definition, "impact.areaHitEvent.maxAcceptedHits", JsonValue.Create(inherited > 0 ? inherited : 1));
            }

            if (!HasExplicitEdit(edits, "impact.areaHitEvent.hitSameTargetPolicy")
                && !HitRepeatKinds.Contains(Text(definition, "impact.areaHitEvent.hitSameTargetPolicy")))
            {
                string inherited = Text(definition, "impact.hitEvent.hitSameTargetPolicy");
                Put(definition, "impact.areaHitEvent.hitSameTargetPolicy", JsonValue.Create(HitRepeatKinds.Contains(inherited) ? inherited : "AllowRepeat"));
            }

            if (!HasExplicitEdit(edits, "impact.areaHitEvent.damage")
                && At(definition, "impact.areaHitEvent.damage") is not JsonObject)
            {
                JsonNode damage = At(definition, "impact.hitEvent.damage")?.DeepClone()
                    ?? new JsonObject
                    {
                        ["damageTypeId"] = "Default",
                        ["baseDamage"] = 0,
                        ["impactPower"] = 0,
                        ["launchPower"] = 0,
                        ["tags"] = new JsonArray(),
                        ["analysisCategory"] = string.Empty,
                    };
                Put(definition, "impact.areaHitEvent.damage", damage);
            }
            NormalizeAreaDamage(definition, edits);

            if (!HasExplicitEdit(edits, "impact.areaHitEvent.onHitBuffs")
                && !ValidBuffList(At(definition, "impact.areaHitEvent.onHitBuffs")))
            {
                JsonNode buffs = ValidBuffList(At(definition, "impact.hitEvent.onHitBuffs"))
                    ? At(definition, "impact.hitEvent.onHitBuffs")?.DeepClone()
                    : new JsonArray();
                Put(definition, "impact.areaHitEvent.onHitBuffs", buffs);
            }
        }

        private static void NormalizeAreaDamage(JsonObject definition, List<ProjectileAuthoringFieldEdit> edits)
        {
            const string areaPath = "impact.areaHitEvent.damage";
            JsonObject area = At(definition, areaPath) as JsonObject;
            if (area == null)
                return;

            JsonObject direct = At(definition, "impact.hitEvent.damage") as JsonObject;
            if (!HasExplicitEdit(edits, areaPath + ".damageTypeId")
                && string.IsNullOrWhiteSpace(Text(definition, areaPath + ".damageTypeId")))
            {
                if (direct != null && direct["damageTypeId"] != null)
                {
                    Put(definition, areaPath + ".damageTypeId", direct["damageTypeId"].DeepClone());
                    if (direct["damageTypeLabel"] != null)
                        Put(definition, areaPath + ".damageTypeLabel", direct["damageTypeLabel"].DeepClone());
                }
                else
                {
                    Put(definition, areaPath + ".damageTypeId", JsonValue.Create(1));
                    Put(definition, areaPath + ".damageTypeLabel", JsonValue.Create("Default"));
                }
            }

            NormalizeAreaDamageNumber(definition, edits, direct, "baseDamage");
            NormalizeAreaDamageNumber(definition, edits, direct, "impactPower");
            NormalizeAreaDamageNumber(definition, edits, direct, "launchPower");

            if (!HasExplicitEdit(edits, areaPath + ".tags")
                && !ValidStringList(At(definition, areaPath + ".tags")))
            {
                if (direct != null && direct["tags"] != null)
                {
                    Put(definition, areaPath + ".tags", direct["tags"].DeepClone());
                    if (direct["tagLabels"] != null)
                        Put(definition, areaPath + ".tagLabels", direct["tagLabels"].DeepClone());
                }
                else
                {
                    Put(definition, areaPath + ".tags", new JsonArray());
                    Put(definition, areaPath + ".tagLabels", new JsonArray());
                }
            }

            if (!HasExplicitEdit(edits, areaPath + ".analysisCategory")
                && At(definition, areaPath + ".analysisCategory") == null)
            {
                if (direct != null && direct["analysisCategory"] != null)
                {
                    Put(definition, areaPath + ".analysisCategory", direct["analysisCategory"].DeepClone());
                    if (direct["analysisCategoryLabel"] != null)
                        Put(definition, areaPath + ".analysisCategoryLabel", direct["analysisCategoryLabel"].DeepClone());
                }
                else
                {
                    Put(definition, areaPath + ".analysisCategory", JsonValue.Create(0));
                    Put(definition, areaPath + ".analysisCategoryLabel", JsonValue.Create(string.Empty));
                }
            }
        }

        private static void NormalizeAreaDamageNumber(
            JsonObject definition,
            List<ProjectileAuthoringFieldEdit> edits,
            JsonObject direct,
            string field)
        {
            string areaPath = "impact.areaHitEvent.damage." + field;
            if (HasExplicitEdit(edits, areaPath) || Integer(definition, areaPath) >= 0)
                return;
            int inherited = direct == null ? 0 : Integer(definition, "impact.hitEvent.damage." + field);
            Put(definition, areaPath, JsonValue.Create(inherited >= 0 ? inherited : 0));
        }

        private static void NormalizeRequest(JsonObject spawn, List<ProjectileAuthoringFieldEdit> edits)
        {
            if (!Has(edits, "aim.kind")) return;
            string kind = Text(spawn, "aim.kind");
            if (!Has(edits, "aim.fallbackKind")) Put(spawn, "aim.fallbackKind", JsonValue.Create("CasterForwardRange"));
            if (string.Equals(kind, "CasterForwardRange", StringComparison.Ordinal))
                Put(spawn, "aim.targetHeightOffset", JsonValue.Create(0f));
        }

        private static void ValidateDefinition(JsonObject node, ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        {
            string motion = Text(node, "motion.kind");
            if (!MotionKinds.Contains(motion)) Range(result, source, id, "motion.kind", "Unsupported motion kind.");
            if (Number(node, "motion.speedPerFrame") <= 0f) Range(result, source, id, "motion.speedPerFrame", "Speed must be positive.");
            if (Integer(node, "motion.lifetimeFrames") <= 0) Range(result, source, id, "motion.lifetimeFrames", "Lifetime must be positive.");
            if (!SpeedKinds.Contains(Text(node, "motion.speedProfile.kind"))) Range(result, source, id, "motion.speedProfile.kind", "Unsupported speed profile.");
            if (string.Equals(motion, "ArcToTarget", StringComparison.Ordinal) && Number(node, "motion.arcHeight") <= 0f) Combination(result, source, id, "motion.arcHeight", "ArcToTarget requires positive arcHeight.");
            if (!string.Equals(motion, "ArcToTarget", StringComparison.Ordinal) && Number(node, "motion.arcHeight") < 0f) Range(result, source, id, "motion.arcHeight", "Arc height cannot be negative.");
            if (string.Equals(motion, "BallisticArcToTarget", StringComparison.Ordinal) && (Number(node, "motion.gravityPerFrameSquared") <= 0f || Number(node, "motion.maxLaunchAngleDegrees") < 0f || Number(node, "motion.maxLaunchAngleDegrees") >= 90f)) Combination(result, source, id, "motion.gravityPerFrameSquared", "Ballistic motion requires positive gravity and launch angle in [0, 90).");
            if (!string.Equals(motion, "BallisticArcToTarget", StringComparison.Ordinal) && Number(node, "motion.gravityPerFrameSquared") < 0f) Range(result, source, id, "motion.gravityPerFrameSquared", "Gravity cannot be negative.");
            if (string.Equals(motion, "SwayForward", StringComparison.Ordinal) && (Number(node, "motion.lateralAmplitude") <= 0f || Integer(node, "motion.lateralCycles") <= 0 || Integer(node, "motion.lateralCycles") > 1000)) Combination(result, source, id, "motion.lateralAmplitude", "SwayForward requires positive amplitude and 1..1000 cycles.");
            if (!string.Equals(motion, "SwayForward", StringComparison.Ordinal) && (Number(node, "motion.lateralAmplitude") < 0f || Integer(node, "motion.lateralCycles") < 0)) Range(result, source, id, "motion.lateralAmplitude", "Lateral values cannot be negative.");
            if (string.Equals(motion, "HomingToTarget", StringComparison.Ordinal) && (Number(node, "motion.homing.turnRatePerFrame") <= 0f || Number(node, "motion.homing.turnRatePerFrame") > 1f)) Combination(result, source, id, "motion.homing.turnRatePerFrame", "Homing motion requires turn rate in (0, 1].");
            if (string.Equals(motion, "BezierToTarget", StringComparison.Ordinal) && ZeroVector(node, "motion.bezierControlOffsetA") && ZeroVector(node, "motion.bezierControlOffsetB")) Combination(result, source, id, "motion.bezierControlOffsetA", "Bezier motion requires a non-zero control offset.");
            if (Number(node, "collision.radius") < 0f) Range(result, source, id, "collision.radius", "Collision radius cannot be negative.");
            if (Integer(node, "collision.maxAcceptedImpacts") <= 0) Range(result, source, id, "collision.maxAcceptedImpacts", "Max accepted impacts must be positive.");
            if (!CollisionRepeatKinds.Contains(Text(node, "collision.hitSameTargetPolicy"))) Range(result, source, id, "collision.hitSameTargetPolicy", "Unsupported collision repeat policy.");
            string impact = Text(node, "impact.policyKind");
            if (!ImpactKinds.Contains(impact)) Range(result, source, id, "impact.policyKind", "Unsupported impact policy.");
            if (string.Equals(impact, "AreaPulseOnImpact", StringComparison.Ordinal) && Number(node, "impact.areaRadius") <= 0f) Combination(result, source, id, "impact.areaRadius", "Area impact requires positive areaRadius.");
            if (!string.Equals(impact, "AreaPulseOnImpact", StringComparison.Ordinal) && Boolean(node, "impact.triggerOnExpire")) Combination(result, source, id, "impact.triggerOnExpire", "triggerOnExpire requires AreaPulseOnImpact.");
            List<string> directTargets = GetStrings(node, "impact.hitEvent.targetTypes");
            bool noDirectSweep = Boolean(node, "physicalEventsEnabled")
                && Number(node, "collision.radius") > 0f
                && (directTargets.Count == 0 || (directTargets.Count == 1 && string.Equals(directTargets[0], "None", StringComparison.Ordinal)));
            ValidateHit(node, "impact.hitEvent", result, source, id, true, noDirectSweep);
            if (string.Equals(impact, "AreaPulseOnImpact", StringComparison.Ordinal)) ValidateHit(node, "impact.areaHitEvent", result, source, id, false);
            string timeline = Text(node, "impact.effectTimelineLabel");
            if (timeline.Length > 0 && !(workspace.AbilityDocument.EffectTimelines ?? new List<AbilityEffectTimelineConfig>()).Any(value => string.Equals(value?.TimelineLabel, timeline, StringComparison.Ordinal))) Combination(result, source, id, "impact.effectTimelineLabel", "The effect timeline reference is unknown in this ability source.");
        }

        private static void ValidateHit(JsonObject node, string path, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id, bool requireDamage, bool allowEmptyTargets = false)
        {
            if (At(node, path) is not JsonObject) { Combination(result, source, id, path, "Hit event object is required."); return; }
            List<string> targets = GetStrings(node, path + ".targetTypes");
            if (targets.Count == 0 && !allowEmptyTargets) Combination(result, source, id, path + ".targetTypes", "At least one target type is required.");
            if (!EnvironmentKinds.Contains(Text(node, path + ".environmentHitPolicy"))) Range(result, source, id, path + ".environmentHitPolicy", "Unsupported environment policy.");
            if (!HitRepeatKinds.Contains(Text(node, path + ".hitSameTargetPolicy"))) Range(result, source, id, path + ".hitSameTargetPolicy", "Unsupported hit repeat policy.");
            if (Integer(node, path + ".maxAcceptedHits") <= 0) Range(result, source, id, path + ".maxAcceptedHits", "Max accepted hits must be positive.");
            JsonNode damage = At(node, path + ".damage");
            if (requireDamage && damage is not JsonObject) Combination(result, source, id, path + ".damage", "Damage is required.");
            if (damage is JsonObject)
            {
                if (string.IsNullOrWhiteSpace(Text(node, path + ".damage.damageTypeId"))) Combination(result, source, id, path + ".damage.damageTypeId", "Damage type is required.");
                if (Integer(node, path + ".damage.baseDamage") < 0 || Integer(node, path + ".damage.impactPower") < 0 || Integer(node, path + ".damage.launchPower") < 0) Range(result, source, id, path + ".damage", "Damage values cannot be negative.");
            }
        }

        private static void ValidateRequest(JsonObject node, ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id)
        {
            string definition = Text(node, "projectileStableId");
            if (!workspace.ReferenceIndex.DefinitionsByStableId.ContainsKey(definition)) Combination(result, source, id, "projectileStableId", "The referenced projectile definition is unknown.");
            string origin = Text(node, "origin.kind");
            if (string.IsNullOrWhiteSpace(origin)) origin = "Socket";
            if (!OriginKinds.Contains(origin)) Range(result, source, id, "origin.kind", "Unsupported origin kind.");
            string socket;
            JsonNode sockNode = At(node, "origin.socketId");
            if (sockNode is JsonValue sockVal && sockVal.TryGetValue(out string sockStr) && !int.TryParse(sockStr, out _))
            {
                socket = sockStr;
            }
            else
            {
                socket = Text(node, "origin.socketLabel");
                if (string.IsNullOrWhiteSpace(socket)) socket = Text(node, "origin.socketId");
            }
            if (string.IsNullOrWhiteSpace(socket)) Combination(result, source, id, "origin.socketId", "Socket origin requires a socket ID.");
            List<string> sockets = workspace.AbilityDocument.KnownSocketIds ?? new List<string>();
            if (sockets.Count > 0 && !sockets.Contains(socket, StringComparer.Ordinal)) Combination(result, source, id, "origin.socketId", "The socket is not present in knownSocketIds.");
            string aim = Text(node, "aim.kind");
            string fallback = Text(node, "aim.fallbackKind");
            if (string.IsNullOrWhiteSpace(aim)) aim = "CasterForwardRange";
            if (string.IsNullOrWhiteSpace(fallback)) fallback = "CasterForwardRange";
            if (!AimKinds.Contains(aim)) Range(result, source, id, "aim.kind", "Unsupported aim kind.");
            if (!AimKinds.Contains(fallback)) Range(result, source, id, "aim.fallbackKind", "Unsupported aim fallback.");
            if (!string.Equals(aim, "CasterForwardRange", StringComparison.Ordinal) && !string.Equals(fallback, "CasterForwardRange", StringComparison.Ordinal)) Combination(result, source, id, "aim.fallbackKind", "Locked target aim requires CasterForwardRange fallback.");
            if (Number(node, "aim.maxRange") < 0f) Range(result, source, id, "aim.maxRange", "Aim maxRange cannot be negative.");
        }

        private static void BuildProjected(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, JsonObject root, string id, bool definition)
        {
            ProjectileAuthoringWorkspaceProjection projection = ProjectileAuthoringWorkspaceProjectionBuilder.Build(workspace, workspace.Compile());
            JsonElement raw;
            using (JsonDocument json = JsonDocument.Parse(root.ToJsonString())) raw = json.RootElement.GetProperty(definition ? "definition" : "spawnProfile").Clone();
            if (definition)
            {
                ProjectileAuthoringWorkspaceDefinition item = projection.Definitions.First(value => string.Equals(value.StableId, id, StringComparison.Ordinal));
                item.Detail = JsonSerializer.Deserialize<ProjectileDefinitionConfig>(raw.GetRawText(), ReadOptions); item.RawJson = raw.GetRawText();
                item.Motion = item.Detail?.Motion?.Kind ?? string.Empty;
                item.Impact = item.Detail?.Impact?.PolicyKind ?? string.Empty;
                if (item.Detail?.Collision != null)
                    item.Detail.Collision.LayerMask = ResolveLayerMask(GetStrings(root["definition"] as JsonObject, "impact.hitEvent.targetTypes"));
                if (item.Detail?.Impact != null)
                {
                    item.Detail.Impact.AreaLayerMask = string.Equals(item.Detail.Impact.PolicyKind, "AreaPulseOnImpact", StringComparison.Ordinal)
                        ? ResolveLayerMask(GetStrings(root["definition"] as JsonObject, "impact.areaHitEvent.targetTypes"))
                        : 0;
                }
            }
            else
            {
                ProjectileAuthoringWorkspaceRequest item = projection.Requests.First(value => string.Equals(value.StableId, id, StringComparison.Ordinal));
                string previousDefinitionStableId = item.DefinitionStableId;
                item.Detail = JsonSerializer.Deserialize<ProjectileSpawnProfileConfig>(raw.GetRawText(), ReadOptions);
                if (item.Detail?.Origin != null && string.IsNullOrEmpty(item.Detail.Origin.SocketLabel) && raw.TryGetProperty("origin", out var origProp) && origProp.TryGetProperty("socketId", out var sidProp) && sidProp.ValueKind == JsonValueKind.String)
                    item.Detail.Origin.SocketLabel = sidProp.GetString() ?? string.Empty;
                item.RawJson = raw.GetRawText();
                item.DefinitionStableId = item.Detail?.ProjectileStableId ?? string.Empty;
                UpdateProjectedDefinitionRequests(projection, id, previousDefinitionStableId, item.DefinitionStableId);
            }
            ProjectileAuthoringWorkspaceSource projectedSource = projection.Sources.First(value => string.Equals(value.RelativePath, source.RelativePath, StringComparison.Ordinal));
            projectedSource.ExpectedSha256 = ProjectileAuthoringHashes.ComputeSha256(Encoding.UTF8.GetBytes(root.ToJsonString()));
            projection.WorkspaceHash = ProjectileAuthoringHashes.ComputeSha256(Encoding.UTF8.GetBytes(string.Join("\n", projection.Sources.OrderBy(value => value.RelativePath, StringComparer.Ordinal).Select(value => value.RelativePath + ":" + value.ExpectedSha256))));
            result.ProjectedWorkspace = projection;
        }

        private static void UpdateProjectedDefinitionRequests(
            ProjectileAuthoringWorkspaceProjection projection,
            string requestStableId,
            string previousDefinitionStableId,
            string currentDefinitionStableId)
        {
            if (string.Equals(previousDefinitionStableId, currentDefinitionStableId, StringComparison.Ordinal))
                return;

            for (int i = 0; i < projection.Definitions.Count; i++)
            {
                ProjectileAuthoringWorkspaceDefinition definition = projection.Definitions[i];
                bool wasReferenced = string.Equals(definition.StableId, previousDefinitionStableId, StringComparison.Ordinal);
                bool isReferenced = string.Equals(definition.StableId, currentDefinitionStableId, StringComparison.Ordinal);
                if (!wasReferenced && !isReferenced)
                    continue;

                if (wasReferenced)
                    definition.RequestIds.RemoveAll(value => string.Equals(value, requestStableId, StringComparison.Ordinal));
                if (isReferenced && !definition.RequestIds.Contains(requestStableId, StringComparer.Ordinal))
                    definition.RequestIds.Add(requestStableId);
                definition.RequestIds.Sort(StringComparer.Ordinal);
                definition.Shared = definition.RequestIds.Count > 1;
            }
        }

        private static void AddDraft(ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, JsonObject root)
        {
            using JsonDocument json = JsonDocument.Parse(root.ToJsonString());
            result.Draft.Documents.Add(new ProjectileAuthoringDraftDocument { Operation = "Upsert", SourceRelativePath = source.RelativePath, ExpectedSha256 = source.Sha256, Json = json.RootElement.Clone() });
        }
        private static JsonObject ParseRoot(ProjectileAuthoringSourceDocument source, ProjectileAuthoringMutationResult result, string id)
        { try { return JsonNode.Parse(source.OriginalUtf8Bytes) as JsonObject; } catch (JsonException ex) { Error(result, "PROJAUTH028", id, string.Empty, ex.Message, source); return null; } }
        private static string ComputeWorkspaceToken(ProjectileAuthoringWorkspace workspace) => ProjectileAuthoringHashes.ComputeSha256(Encoding.UTF8.GetBytes(string.Join("\n", workspace.SourceDocuments.OrderBy(value => value.RelativePath, StringComparer.Ordinal).Select(value => value.RelativePath + ":" + value.Sha256))));
        private static bool Kind(ProjectileAuthoringFieldEdit edit, string expected) => string.Equals(edit?.ValueKind, expected, StringComparison.OrdinalIgnoreCase);
        private static bool Type(ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id, string path, string expected) { Error(result, "PROJAUTH026", id, path, "Field value kind must be " + expected + ".", source); return true; }
        private static void Range(ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id, string path, string message) => Error(result, "PROJAUTH027", id, path, message, source);
        private static void Combination(ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string id, string path, string message) => Error(result, "PROJAUTH028", id, path, message, source);
        private static void Error(ProjectileAuthoringMutationResult result, string code, string id, string path, string message, ProjectileAuthoringSourceDocument source = null) => result.Diagnostics.Add(new ProjectileAuthoringDiagnostic { Code = code, Severity = "Error", Message = message, SourcePath = source?.RelativePath ?? string.Empty, ObjectPath = id, FieldPath = path, RelatedStableIds = new List<string> { id } });
        private static HashSet<string> Set(params string[] values) => new HashSet<string>(values, StringComparer.Ordinal);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Has(List<ProjectileAuthoringFieldEdit> edits, string path) => edits != null && edits.Any(value => string.Equals(value?.FieldPath, path, StringComparison.Ordinal));
        private static bool HasExplicitEdit(List<ProjectileAuthoringFieldEdit> edits, string path) => edits != null && edits.Any(value =>
            string.Equals(value?.FieldPath, path, StringComparison.Ordinal)
            || (value?.FieldPath?.StartsWith(path + ".", StringComparison.Ordinal) ?? false)
            || path.StartsWith((value?.FieldPath ?? string.Empty) + ".", StringComparison.Ordinal));
        private static void Put(JsonObject root, string path, JsonNode value)
        { string[] parts = path.Split('.'); JsonObject parent = root; for (int i = 0; i < parts.Length - 1; i++) { if (parent[parts[i]] is not JsonObject child) { child = new JsonObject(); parent[parts[i]] = child; } parent = child; } parent[parts[parts.Length - 1]] = value; }
        private static JsonObject EnsureObject(JsonObject root, string path) { if (At(root, path) is JsonObject value) return value; var output = new JsonObject(); Put(root, path, output); return output; }
        private static JsonNode At(JsonObject root, string path) { JsonNode current = root; string[] parts = path.Split('.'); for (int i = 0; i < parts.Length && current is JsonObject obj; i++) current = obj[parts[i]]; return current; }
        private static string Text(JsonObject root, string path)
        {
            JsonNode node = At(root, path);
            if (node is JsonValue value)
            {
                if (value.TryGetValue(out string text)) return text ?? string.Empty;
                if (value.TryGetValue(out int intVal)) return intVal.ToString();
                if (value.TryGetValue(out long longVal)) return longVal.ToString();
            }
            return string.Empty;
        }
        private static float Number(JsonObject root, string path) { JsonNode node = At(root, path); if (node is JsonValue value && value.TryGetValue(out float number)) return number; if (node is JsonValue integral && integral.TryGetValue(out int integer)) return integer; return 0f; }
        private static int Integer(JsonObject root, string path) { JsonNode node = At(root, path); return node is JsonValue value && value.TryGetValue(out int number) ? number : 0; }
        private static bool Boolean(JsonObject root, string path) { JsonNode node = At(root, path); return node is JsonValue value && value.TryGetValue(out bool flag) && flag; }
        private static List<string> GetStrings(JsonObject root, string path) { var values = new List<string>(); if (At(root, path) is JsonArray array) for (int i = 0; i < array.Count; i++) if (array[i] is JsonValue item && item.TryGetValue(out string text)) values.Add(text); return values; }
        private static bool ZeroVector(JsonObject root, string path) => Math.Abs(Number(root, path + ".x")) < .0001f && Math.Abs(Number(root, path + ".y")) < .0001f && Math.Abs(Number(root, path + ".z")) < .0001f;
        private static JsonObject VectorNode(float x, float y, float z) => new JsonObject { ["x"] = x, ["y"] = y, ["z"] = z };
        private static bool ValidAreaTargets(List<string> values) => values.Count > 0 && values.All(TargetKinds.Contains) && ResolveLayerMask(values) != 0;
        private static bool ValidStringList(JsonNode node)
        {
            if (node is not JsonArray values)
                return false;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] is not JsonValue value)
                    return false;
                if (value.TryGetValue(out string text))
                {
                    if (string.IsNullOrWhiteSpace(text)) return false;
                }
                else if (value.TryGetValue(out int num))
                {
                    if (num <= 0) return false;
                }
                else return false;
            }
            return true;
        }
        private static bool ValidBuffList(JsonNode node)
        {
            if (node is not JsonArray values)
                return false;
            var seen = new HashSet<int>();
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] is not JsonObject value
                    || value["buffId"] is not JsonValue buffId
                    || !buffId.TryGetValue(out int id)
                    || id <= 0
                    || !seen.Add(id))
                {
                    return false;
                }
            }
            return true;
        }
        private static int ResolveLayerMask(List<string> values) { int mask = 0; for (int i = 0; i < values.Count; i++) { string value = values[i]; if (string.Equals(value, "WorldWall", StringComparison.Ordinal) || string.Equals(value, "WorldGround", StringComparison.Ordinal) || string.Equals(value, "WorldStatic", StringComparison.Ordinal)) mask |= 1 << 1; else if (!string.Equals(value, "None", StringComparison.Ordinal)) mask |= 1 << 8; } return mask; }
    }
}
