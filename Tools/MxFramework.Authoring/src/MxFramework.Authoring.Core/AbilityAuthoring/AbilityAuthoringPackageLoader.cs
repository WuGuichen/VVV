using System;
using System.IO;
using System.Text.Json;

namespace MxFramework.Authoring
{
    public static class AbilityAuthoringPackageLoader
    {
        public const string ProjectileRequestDirectoryRelativePath = "config/projectiles";

        /// <summary>
        /// Loads an ability authoring package and merges per-projectile request source files.
        /// </summary>
        /// <remarks>LowFreqAlloc: authoring-time package IO and JSON parsing.</remarks>
        public static AbilityAuthoringDocument Load(
            string packageOrDocumentPath,
            JsonSerializerOptions options)
        {
            string documentPath = AbilityAuthoringCompiler.ResolveDocumentPath(packageOrDocumentPath);
            if (!File.Exists(documentPath))
                throw new FileNotFoundException("Ability authoring document was not found.", documentPath);

            AbilityAuthoringDocument document =
                JsonSerializer.Deserialize<AbilityAuthoringDocument>(File.ReadAllText(documentPath), options)
                ?? new AbilityAuthoringDocument();
            string packageRoot = AbilityAuthoringCompiler.ResolvePackageRootPath(packageOrDocumentPath);
            MergeProjectileRequests(
                document,
                Path.Combine(packageRoot, ProjectileRequestDirectoryRelativePath),
                options);
            return document;
        }

        /// <summary>
        /// Merges all projectile request documents from a directory into the existing authoring document.
        /// </summary>
        /// <remarks>LowFreqAlloc: authoring-time package IO and JSON parsing.</remarks>
        public static void MergeProjectileRequests(
            AbilityAuthoringDocument document,
            string projectileRequestDirectory,
            JsonSerializerOptions options)
        {
            if (document == null || string.IsNullOrWhiteSpace(projectileRequestDirectory) || !Directory.Exists(projectileRequestDirectory))
                return;

            document.ProjectileDefinitions ??= new System.Collections.Generic.List<ProjectileDefinitionConfig>();
            document.ProjectileSpawnProfiles ??= new System.Collections.Generic.List<ProjectileSpawnProfileConfig>();
            document.ProjectilePresentationBindings ??= new System.Collections.Generic.List<ProjectilePresentationBindingConfig>();
            string[] files = Directory.GetFiles(projectileRequestDirectory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
                MergeProjectileRequest(document, files[i], options);
        }

        private static void MergeProjectileRequest(
            AbilityAuthoringDocument document,
            string path,
            JsonSerializerOptions options)
        {
            string json;
            ProjectileRequestAuthoringDocument request = null;
            try
            {
                json = File.ReadAllText(path);
                ValidateProjectileRequestSourceShape(document, path, json);
                request = JsonSerializer.Deserialize<ProjectileRequestAuthoringDocument>(
                    json,
                    options);
                if (request != null)
                {
                    using JsonDocument doc = JsonDocument.Parse(json);
                    PopulateLegacyLabels(request, doc.RootElement);
                }
            }
            catch (Exception ex)
            {
                AddError(
                    document,
                    "ABILITY_PROJECTILE_REQUEST_JSON_INVALID",
                    path,
                    "投射物请求配置 JSON 读取失败：" + ex.Message);
                return;
            }

            if (request == null)
            {
                AddError(
                    document,
                    "ABILITY_PROJECTILE_REQUEST_NULL",
                    path,
                    "投射物请求配置不能为空。");
                return;
            }

            request.SchemaVersion = string.IsNullOrWhiteSpace(request.SchemaVersion) ? "1" : request.SchemaVersion;
            request.StableId ??= string.Empty;
            string fileStableId = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(request.SchemaVersion, "1", StringComparison.Ordinal))
            {
                AddError(
                    document,
                    "ABILITY_PROJECTILE_REQUEST_SCHEMA_UNSUPPORTED",
                    path + ".schemaVersion",
                    "投射物请求配置 schemaVersion 必须为 1。");
            }

            if (!string.Equals(fileStableId, request.StableId, StringComparison.Ordinal))
            {
                AddError(
                    document,
                    "ABILITY_PROJECTILE_REQUEST_FILE_STABLE_ID_MISMATCH",
                    path + ".stableId",
                    "投射物请求配置文件名必须等于 stableId：" + fileStableId + " != " + request.StableId + "。");
            }

            if (request.SpawnProfile != null)
            {
                if (!string.Equals(request.StableId, request.SpawnProfile.StableId ?? string.Empty, StringComparison.Ordinal))
                {
                    AddError(
                        document,
                        "ABILITY_PROJECTILE_REQUEST_SPAWN_STABLE_ID_MISMATCH",
                        path + ".spawnProfile.stableId",
                        "投射物请求 stableId 必须等于 spawnProfile.stableId。");
                }

                if (request.Definition != null
                    && !string.Equals(request.SpawnProfile.ProjectileStableId ?? string.Empty, request.Definition.StableId ?? string.Empty, StringComparison.Ordinal))
                {
                    AddError(
                        document,
                        "ABILITY_PROJECTILE_REQUEST_PROJECTILE_STABLE_ID_MISMATCH",
                        path + ".spawnProfile.projectileStableId",
                        "spawnProfile.projectileStableId 必须等于 definition.stableId。");
                }

                document.ProjectileSpawnProfiles.Add(request.SpawnProfile);
            }

            if (request.Definition != null)
                document.ProjectileDefinitions.Add(request.Definition);

            if (request.PresentationBinding != null)
            {
                if (request.Definition != null
                    && request.PresentationBinding.ProjectileDefinitionId != request.Definition.DefinitionId)
                {
                    AddError(
                        document,
                        "ABILITY_PROJECTILE_REQUEST_PRESENTATION_DEFINITION_MISMATCH",
                        path + ".presentationBinding.projectileDefinitionId",
                        "presentationBinding.projectileDefinitionId 必须等于 definition.definitionId。");
                }

                document.ProjectilePresentationBindings.Add(request.PresentationBinding);
            }
        }

        private static void ValidateProjectileRequestSourceShape(
            AbilityAuthoringDocument document,
            string path,
            string json)
        {
            if (document == null || string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                using JsonDocument parsed = JsonDocument.Parse(json);
                if (parsed.RootElement.ValueKind != JsonValueKind.Object || !parsed.RootElement.TryGetProperty("definition", out JsonElement definition) || definition.ValueKind != JsonValueKind.Object)
                    return;

                if (definition.TryGetProperty("collision", out JsonElement collision)
                    && collision.ValueKind == JsonValueKind.Object
                    && collision.TryGetProperty("layerMask", out _))
                {
                    AddError(
                        document,
                        "ABILITY_PROJECTILE_REQUEST_COLLISION_LAYER_MASK_RAW",
                        path + ".definition.collision.layerMask",
                        "投射物请求 source 不能直接写 collision.layerMask，请通过 impact.hitEvent.targetTypes 选择碰撞目标。");
                }

                if (definition.TryGetProperty("impact", out JsonElement impact)
                    && impact.ValueKind == JsonValueKind.Object
                    && impact.TryGetProperty("areaLayerMask", out _))
                {
                    AddError(
                        document,
                        "ABILITY_PROJECTILE_REQUEST_AREA_LAYER_MASK_RAW",
                        path + ".definition.impact.areaLayerMask",
                        "投射物请求 source 不能直接写 impact.areaLayerMask，请通过 impact.areaHitEvent.targetTypes 选择范围目标。");
                }
            }
            catch (JsonException)
            {
                // The deserialize path reports the parse error with the same source path.
            }
        }

        internal static void PopulateLegacyLabels(ProjectileRequestAuthoringDocument projection, JsonElement root)
        {
            if (projection?.Definition?.Impact == null || root.ValueKind != JsonValueKind.Object) return;
            if (root.TryGetProperty("definition", out var defEl) && defEl.ValueKind == JsonValueKind.Object && defEl.TryGetProperty("impact", out var impEl) && impEl.ValueKind == JsonValueKind.Object)
            {
                if (impEl.TryGetProperty("presentationProfileId", out var pidEl) && pidEl.ValueKind == JsonValueKind.String)
                {
                    string s = pidEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && string.IsNullOrEmpty(projection.Definition.Impact.PresentationProfileLabel))
                        projection.Definition.Impact.PresentationProfileLabel = s;
                }
                if (impEl.TryGetProperty("effectTimelineId", out var tidEl) && tidEl.ValueKind == JsonValueKind.String)
                {
                    string s = tidEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && string.IsNullOrEmpty(projection.Definition.Impact.EffectTimelineLabel))
                        projection.Definition.Impact.EffectTimelineLabel = s;
                }
                if (impEl.TryGetProperty("stableEventId", out var sidEl) && sidEl.ValueKind == JsonValueKind.String)
                {
                    string s = sidEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && string.IsNullOrEmpty(projection.Definition.Impact.StableEventLabel))
                        projection.Definition.Impact.StableEventLabel = s;
                }
                if (impEl.TryGetProperty("hitEvent", out var hitEl) && hitEl.ValueKind == JsonValueKind.Object)
                {
                    if (hitEl.TryGetProperty("stableEventId", out var hsidEl) && hsidEl.ValueKind == JsonValueKind.String)
                    {
                        string s = hsidEl.GetString();
                        if (!string.IsNullOrWhiteSpace(s) && projection.Definition.Impact.HitEvent != null && string.IsNullOrEmpty(projection.Definition.Impact.HitEvent.StableEventLabel))
                            projection.Definition.Impact.HitEvent.StableEventLabel = s;
                    }
                    if (hitEl.TryGetProperty("damage", out var dmgEl) && dmgEl.ValueKind == JsonValueKind.Object)
                    {
                        if (dmgEl.TryGetProperty("damageTypeId", out var dtEl) && dtEl.ValueKind == JsonValueKind.String)
                        {
                            string s = dtEl.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && projection.Definition.Impact.HitEvent?.Damage != null && string.IsNullOrEmpty(projection.Definition.Impact.HitEvent.Damage.DamageTypeLabel))
                                projection.Definition.Impact.HitEvent.Damage.DamageTypeLabel = s;
                        }
                        if (dmgEl.TryGetProperty("analysisCategory", out var acEl) && acEl.ValueKind == JsonValueKind.String)
                        {
                            string s = acEl.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && projection.Definition.Impact.HitEvent?.Damage != null && string.IsNullOrEmpty(projection.Definition.Impact.HitEvent.Damage.AnalysisCategoryLabel))
                                projection.Definition.Impact.HitEvent.Damage.AnalysisCategoryLabel = s;
                        }
                    }
                }
            }
            if (root.TryGetProperty("spawnProfile", out var spawnEl) && spawnEl.ValueKind == JsonValueKind.Object && spawnEl.TryGetProperty("origin", out var origEl) && origEl.ValueKind == JsonValueKind.Object)
            {
                if (origEl.TryGetProperty("socketId", out var sockEl) && sockEl.ValueKind == JsonValueKind.String)
                {
                    string s = sockEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && projection.SpawnProfile?.Origin != null && string.IsNullOrEmpty(projection.SpawnProfile.Origin.SocketLabel))
                        projection.SpawnProfile.Origin.SocketLabel = s;
                }
            }
        }

        private static void AddError(
            AbilityAuthoringDocument document,
            string code,
            string path,
            string message)
        {
            document.SourceDiagnostics ??= new System.Collections.Generic.List<AbilityAuthoringDiagnostic>();
            document.SourceDiagnostics.Add(new AbilityAuthoringDiagnostic
            {
                Severity = "Error",
                Code = code ?? string.Empty,
                Path = path ?? string.Empty,
                Message = message ?? string.Empty,
            });
        }
    }
}
