using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MxFramework.Authoring
{
    /// <summary>Plans allowlisted projectile Presentation Binding v2 edits without writing files.</summary>
    /// <remarks>LowFreqAlloc: JSON clones and projected workspaces are created only for authoring commands.</remarks>
    public static class ProjectileAuthoringPresentationEditor
    {
        private static readonly string[] SlotKinds = { "spawn", "flightBody", "trail", "directImpact", "areaImpact", "blockedImpact", "expire" };

        /// <summary>Plans one slot, exit-policy, or explicit v1 migration command.</summary>
        /// <remarks>LowFreqAlloc: interactive authoring transaction planning.</remarks>
        public static void Edit(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result)
        {
            string stableId = request.SourceStableId ?? string.Empty;
            if (!workspace.ReferenceIndex.DefinitionsByStableId.TryGetValue(stableId, out ProjectileDefinitionConfig definition))
            {
                Error(result, "PROJAUTH012", stableId, "presentationBinding", "Projectile definition was not found.");
                return;
            }
            ProjectileAuthoringSourceDocument[] matches = workspace.SourceDocuments.Where(value => value.Projection?.PresentationBinding?.ProjectileDefinitionId == definition.DefinitionId).ToArray();
            ProjectileAuthoringSourceDocument source = matches.Length == 1
                ? matches[0]
                : workspace.SourceDocuments.SingleOrDefault(value => value.Projection?.Definition?.DefinitionId == definition.DefinitionId);
            if (source == null || matches.Length > 1)
            {
                Error(result, "PROJAUTH004", stableId, "presentationBinding", "Canonical presentation source could not be located unambiguously.");
                return;
            }
            if (!string.Equals(request.Kind, "MigratePresentationBindingV1ToV2", StringComparison.Ordinal)
                && source.Projection?.PresentationBinding?.PrefabResource != null)
            {
                Error(result, "PROJAUTH013", stableId, "presentationBinding.prefabResource", "Legacy Presentation Binding v1 is read-only. Run the explicit migration command before editing slots or exit policy.");
                return;
            }

            string fieldPath = ResolveFieldPath(request);
            PrepareImpact(workspace, request, result, stableId, fieldPath);
            if (request.PlanOnly) return;
            if (result.Impact.RequestStableIds.Count > 1 && (!request.Confirmed || !string.Equals(request.ConfirmationToken, result.Impact.ConfirmationToken, StringComparison.Ordinal)))
            {
                Error(result, "PROJAUTH029", stableId, fieldPath, "Shared definition confirmation is missing or stale.");
                return;
            }

            JsonObject root = JsonNode.Parse(source.OriginalUtf8Bytes)?.AsObject();
            if (root == null) { Error(result, "PROJAUTH005", stableId, fieldPath, "Projectile source root is invalid."); return; }
            JsonObject binding = root["presentationBinding"] as JsonObject;
            if (binding == null)
            {
                binding = new JsonObject { ["projectileDefinitionId"] = definition.DefinitionId, ["viewNamePrefix"] = "ProjectileVFX" };
                root["presentationBinding"] = binding;
            }

            switch (request.Kind)
            {
                case "MigratePresentationBindingV1ToV2": Migrate(binding, result, source, stableId); break;
                case "SetPresentationSlot": SetSlot(workspace, binding, request, result, stableId); break;
                case "ClearPresentationSlot": ClearSlot(binding, request, result, source, stableId); break;
                case "EditPresentationExitPolicy": SetExit(binding, request, result, source, stableId); break;
            }
            if (result.Diagnostics.Any(value => string.Equals(value.Severity, "Error", StringComparison.OrdinalIgnoreCase))) return;
            if (string.Equals(request.Kind, "SetPresentationSlot", StringComparison.Ordinal)
                && request.SynchronizeVfxContractLifetime)
                SynchronizeVfxContractLifetime(workspace, definition, request, result);
            if (result.Diagnostics.Any(value => string.Equals(value.Severity, "Error", StringComparison.OrdinalIgnoreCase))) return;
            EnsureExitPolicy(binding);
            using JsonDocument json = JsonDocument.Parse(root.ToJsonString());
            result.Draft.Documents.Add(new ProjectileAuthoringDraftDocument { Operation = "Upsert", SourceRelativePath = source.RelativePath, ExpectedSha256 = source.Sha256, Json = json.RootElement.Clone() });
            BuildProjected(workspace, result, source, root, stableId, definition.DefinitionId);
            result.Impact.Blocking = false;
            result.Impact.BlockingReasons.Clear();
        }

        private static void SynchronizeVfxContractLifetime(
            ProjectileAuthoringWorkspace workspace,
            ProjectileDefinitionConfig projectileDefinition,
            ProjectileAuthoringMutationRequest request,
            ProjectileAuthoringMutationResult result)
        {
            int frames = request.PresentationLifetimeFramesOverride;
            if (frames <= 0)
            {
                Error(result, "PROJAUTH012", projectileDefinition.StableId,
                    "presentationBinding.slots." + request.PresentationSlotKind + ".lifetimeFramesOverride",
                    "Synchronized one-shot VFX contract length must be greater than zero.");
                return;
            }

            VfxDefinitionConfig[] definitions = workspace.VfxDefinitions
                .Where(value => SameResourceKey(value.PrefabResource, request.PresentationResource))
                .ToArray();
            if (definitions.Length != 1)
            {
                Error(result, "PROJAUTH011", projectileDefinition.StableId,
                    "presentationBinding.slots." + request.PresentationSlotKind + ".resource",
                    "Synchronized VFX contract requires exactly one matching VFX definition.");
                return;
            }

            VfxDefinitionConfig vfxDefinition = definitions[0];
            string profileId = projectileDefinition.Impact?.PresentationProfileLabel ?? string.Empty;
            string documentPath = AbilityAuthoringCompiler.ResolveDocumentPath(workspace.PackageRoot);
            if (!File.Exists(documentPath))
            {
                Error(result, "PROJAUTH005", projectileDefinition.StableId, "vfxDefinitions",
                    "Ability authoring source document was not found.");
                return;
            }

            byte[] original = File.ReadAllBytes(documentPath);
            JsonObject document = JsonNode.Parse(original)?.AsObject();
            JsonArray definitionNodes = document?["vfxDefinitions"] as JsonArray;
            JsonArray profileNodes = document?["vfxPresentationProfiles"] as JsonArray;
            JsonObject definitionNode = definitionNodes?.OfType<JsonObject>().SingleOrDefault(
                value => value["definitionId"]?.GetValue<int>() == vfxDefinition.DefinitionId);
            JsonObject profileNode = profileNodes?.OfType<JsonObject>().SingleOrDefault(value =>
                !string.IsNullOrWhiteSpace(profileId)
                    ? string.Equals(value["presentationProfileLabel"]?.GetValue<string>() ?? value["presentationProfileId"]?.ToString(), profileId, StringComparison.Ordinal)
                    : value["definitionId"]?.GetValue<int>() == vfxDefinition.DefinitionId);
            if (definitionNode == null || profileNode == null)
            {
                Error(result, "PROJAUTH005", projectileDefinition.StableId, "vfxPresentationProfiles",
                    "Matching VFX definition or presentation profile source could not be located unambiguously.");
                return;
            }

            definitionNode["defaultLifetimeFrames"] = frames;
            profileNode["lifetimeFramesOverride"] = frames;
            using JsonDocument updated = JsonDocument.Parse(document.ToJsonString());
            string relativePath = Path.GetRelativePath(workspace.PackageRoot, documentPath).Replace('\\', '/');
            result.Draft.Documents.Add(new ProjectileAuthoringDraftDocument
            {
                Operation = "SynchronizeVfxContract",
                SourceRelativePath = relativePath,
                ExpectedSha256 = ProjectileAuthoringHashes.ComputeSha256(original),
                Json = updated.RootElement.Clone()
            });
            result.Impact.FieldPaths.Add("vfxDefinitions[" + vfxDefinition.DefinitionId + "].defaultLifetimeFrames");
            result.Impact.FieldPaths.Add("vfxPresentationProfiles[" + profileId + "].lifetimeFramesOverride");
        }

        private static void Migrate(JsonObject binding, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string stableId)
        {
            if (binding["prefabResource"] is not JsonObject legacy)
            {
                Error(result, "PROJAUTH013", stableId, "presentationBinding.prefabResource", "Presentation Binding is not a migratable v1 record.");
                return;
            }
            JsonObject slots = binding["slots"] as JsonObject ?? new JsonObject();
            if (slots["flightBody"] == null)
            {
                slots["flightBody"] = new JsonObject
                {
                    ["resource"] = legacy.DeepClone(),
                    ["orientation"] = "ProjectileForward",
                    ["localOffset"] = VectorNode(new ProjectileVector3Config()),
                    ["lifetimeFramesOverride"] = 0
                };
            }
            binding["slots"] = slots;
            binding.Remove("prefabResource");
            EnsureExitPolicy(binding);
            if (!string.IsNullOrWhiteSpace(source.Projection?.Definition?.Impact?.PresentationProfileLabel))
                Warning(result, "PROJAUTH014", source.RelativePath, stableId, "definition.impact.presentationProfileId", "Legacy presentationProfileId remains a separate manual migration reference and was not mapped to a VFX slot.");
        }

        private static void SetSlot(ProjectileAuthoringWorkspace workspace, JsonObject binding, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result, string stableId)
        {
            string kind = request.PresentationSlotKind ?? string.Empty;
            if (!SlotKinds.Contains(kind, StringComparer.Ordinal)) { Error(result, "PROJAUTH012", stableId, "presentationBinding.slots", "Unknown projectile presentation slot."); return; }
            AbilityResourceKeyConfig resource = request.PresentationResource;
            if (resource == null || string.IsNullOrWhiteSpace(resource.Id) || !string.Equals(resource.TypeId, "GameObject", StringComparison.Ordinal)) { Error(result, "PROJAUTH012", stableId, "presentationBinding.slots." + kind + ".resource", "A GameObject ResourceKey is required."); return; }
            bool persistent = kind == "flightBody" || kind == "trail";
            if (request.PresentationLifetimeFramesOverride < 0 || (persistent && request.PresentationLifetimeFramesOverride != 0)) { Error(result, "PROJAUTH012", stableId, "presentationBinding.slots." + kind + ".lifetimeFramesOverride", "Lifetime override is invalid for the slot lifecycle."); return; }
            if (!OrientationAllowed(kind, request.PresentationOrientation)) { Error(result, "PROJAUTH012", stableId, "presentationBinding.slots." + kind + ".orientation", "Orientation is incompatible with the fixed slot anchor."); return; }
            if (!persistent && !workspace.VfxDefinitions.Any(value => SameResourceKey(value.PrefabResource, resource)))
            {
                Error(result, "PROJAUTH011", stableId, "presentationBinding.slots." + kind + ".resource", "One-shot VFX ResourceKey does not resolve to a compact VfxDefinitionId.");
                return;
            }
            JsonObject slots = binding["slots"] as JsonObject ?? new JsonObject();
            slots[kind] = new JsonObject
            {
                ["resource"] = new JsonObject
                {
                    ["id"] = resource.Id ?? string.Empty,
                    ["typeId"] = resource.TypeId ?? string.Empty,
                    ["variant"] = resource.Variant ?? string.Empty,
                    ["packageId"] = resource.PackageId ?? string.Empty
                },
                ["orientation"] = request.PresentationOrientation,
                ["localOffset"] = VectorNode(request.PresentationLocalOffset),
                ["lifetimeFramesOverride"] = request.PresentationLifetimeFramesOverride
            };
            binding["slots"] = slots;
            binding.Remove("prefabResource");
        }

        private static void ClearSlot(JsonObject binding, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string stableId)
        {
            string kind = request.PresentationSlotKind ?? string.Empty;
            if (!SlotKinds.Contains(kind, StringComparer.Ordinal)) { Error(result, "PROJAUTH012", stableId, "presentationBinding.slots", "Unknown projectile presentation slot."); return; }
            if (binding["slots"] is JsonObject slots) slots.Remove(kind);
            binding.Remove("prefabResource");
        }

        private static void SetExit(JsonObject binding, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, string stableId)
        {
            string kind = request.PresentationExitKind ?? string.Empty;
            string mode = request.PresentationExitMode ?? string.Empty;
            if (kind != "acceptedImpact" && kind != "blockedImpact" && kind != "expiration" && kind != "externalRemoval") { Error(result, "PROJAUTH012", stableId, "presentationBinding.exitPolicy", "Unknown editable exit policy field."); return; }
            if (mode != "Complete" && mode != "Dissipate") { Error(result, "PROJAUTH012", stableId, "presentationBinding.exitPolicy." + kind, "Impact and removal exits allow only Complete or Dissipate."); return; }
            EnsureExitPolicy(binding);
            ((JsonObject)binding["exitPolicy"])[kind] = mode;
            binding.Remove("prefabResource");
        }

        private static void EnsureExitPolicy(JsonObject binding)
        {
            JsonObject exit = binding["exitPolicy"] as JsonObject ?? new JsonObject();
            exit["acceptedImpact"] ??= "Complete"; exit["blockedImpact"] ??= "Dissipate"; exit["expiration"] ??= "Complete"; exit["externalRemoval"] ??= "Dissipate"; exit["runtimeReset"] = "Immediate";
            binding["exitPolicy"] = exit;
        }

        private static bool OrientationAllowed(string kind, string value)
        {
            if (kind == "spawn" || kind == "expire") return value == "ProjectileForward" || value == "WorldUp";
            if (kind == "flightBody" || kind == "trail") return value == "ProjectileForward";
            if (kind == "directImpact") return value == "SurfaceNormal" || value == "ProjectileForward";
            if (kind == "areaImpact") return value == "ImpactNormalOrWorldUp" || value == "WorldUp";
            return kind == "blockedImpact" && value == "SurfaceNormal";
        }

        private static bool SameResourceKey(AbilityResourceKeyConfig left, AbilityResourceKeyConfig right)
        {
            return left != null && right != null
                && string.Equals(left.Id, right.Id, StringComparison.Ordinal)
                && string.Equals(left.TypeId ?? string.Empty, right.TypeId ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(left.Variant ?? string.Empty, right.Variant ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(left.PackageId ?? string.Empty, right.PackageId ?? string.Empty, StringComparison.Ordinal);
        }

        private static JsonObject VectorNode(ProjectileVector3Config value) => new JsonObject { ["x"] = value?.X ?? 0f, ["y"] = value?.Y ?? 0f, ["z"] = value?.Z ?? 0f };
        private static string ResolveFieldPath(ProjectileAuthoringMutationRequest request) => request.Kind == "EditPresentationExitPolicy" ? "presentationBinding.exitPolicy." + request.PresentationExitKind : request.Kind == "MigratePresentationBindingV1ToV2" ? "presentationBinding" : "presentationBinding.slots." + request.PresentationSlotKind;

        private static void PrepareImpact(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationRequest request, ProjectileAuthoringMutationResult result, string stableId, string fieldPath)
        {
            result.Impact.StableId = stableId; result.Impact.FieldPaths.Add(fieldPath);
            if (workspace.ReferenceIndex.RequestIdsByDefinitionStableId.TryGetValue(stableId, out IReadOnlyList<string> requests)) result.Impact.RequestStableIds.AddRange(requests.OrderBy(value => value, StringComparer.Ordinal));
            string workspaceToken = ProjectileAuthoringWorkspaceProjectionBuilder.Build(workspace, workspace.Compile()).WorkspaceHash;
            result.Impact.WorkspaceToken = workspaceToken;
            result.Impact.ConfirmationToken = ProjectileAuthoringHashes.ComputeSha256(Encoding.UTF8.GetBytes(workspaceToken + "\n" + stableId + "\n" + string.Join("\n", result.Impact.RequestStableIds) + "\n" + fieldPath));
            result.Impact.Summary = result.Impact.RequestStableIds.Count + " request(s) are affected by " + fieldPath + ".";
            if (!string.IsNullOrEmpty(request.WorkspaceToken) && !string.Equals(request.WorkspaceToken, workspaceToken, StringComparison.Ordinal)) Error(result, "PROJAUTH029", stableId, fieldPath, "Workspace token is stale.");
            if (result.Impact.RequestStableIds.Count > 1 && (!request.Confirmed || !string.Equals(request.ConfirmationToken, result.Impact.ConfirmationToken, StringComparison.Ordinal))) { result.Impact.Blocking = true; result.Impact.BlockingReasons.Add("shared-definition-confirmation-required"); }
        }

        private static void BuildProjected(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringMutationResult result, ProjectileAuthoringSourceDocument source, JsonObject root, string stableId, int definitionId)
        {
            ProjectileAuthoringWorkspaceProjection projection = ProjectileAuthoringWorkspaceProjectionBuilder.Build(workspace, workspace.Compile());
            string raw = root["presentationBinding"]?.ToJsonString() ?? string.Empty;
            ProjectileAuthoringWorkspaceBinding item = projection.Bindings.FirstOrDefault(value => value.ProjectileDefinitionId == definitionId);
            if (item == null) { item = new ProjectileAuthoringWorkspaceBinding { ProjectileDefinitionId = definitionId, DefinitionStableId = stableId, SourcePath = source.RelativePath }; projection.Bindings.Add(item); projection.PresentationBindingCount = projection.Bindings.Count; }
            item.Detail = JsonSerializer.Deserialize<ProjectilePresentationBindingConfig>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            AbilityAuthoringCompiler.ResolvePresentationSlotIds(item.Detail, workspace.VfxDefinitions);
            item.RawJson = raw; item.LegacyV1 = raw.Contains("\"prefabResource\"", StringComparison.Ordinal);
            ProjectileAuthoringWorkspaceSource projectedSource = projection.Sources.First(value => string.Equals(value.RelativePath, source.RelativePath, StringComparison.Ordinal));
            projectedSource.ExpectedSha256 = ProjectileAuthoringHashes.ComputeSha256(Encoding.UTF8.GetBytes(root.ToJsonString()));
            projection.WorkspaceHash = ProjectileAuthoringHashes.ComputeSha256(Encoding.UTF8.GetBytes(string.Join("\n", projection.Sources.OrderBy(value => value.RelativePath, StringComparer.Ordinal).Select(value => value.RelativePath + ":" + value.ExpectedSha256))));
            result.ProjectedWorkspace = projection;
        }

        private static void Error(ProjectileAuthoringMutationResult result, string code, string stableId, string path, string message) => result.Diagnostics.Add(new ProjectileAuthoringDiagnostic { Code = code, Severity = "Error", Message = message, ObjectPath = "definition:" + stableId, FieldPath = path, RelatedStableIds = new List<string> { stableId } });
        private static void Warning(ProjectileAuthoringMutationResult result, string code, string sourcePath, string stableId, string path, string message) => result.Diagnostics.Add(new ProjectileAuthoringDiagnostic { Code = code, Severity = "Warning", Message = message, SourcePath = sourcePath, ObjectPath = "definition:" + stableId, FieldPath = path, RelatedStableIds = new List<string> { stableId } });
    }
}
