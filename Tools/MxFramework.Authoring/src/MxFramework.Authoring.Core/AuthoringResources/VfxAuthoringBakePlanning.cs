using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MxFramework.Authoring
{
    public static class VfxAuthoringBakerIds
    {
        public const string ParticleSystemPrefab = "mx.vfx.baker.particle-system-prefab";
        public const string ParticleSystemPrefabVersion = "1";
    }

    /// <summary>Computes deterministic Recipe input hashes for Published Effect stale detection.</summary>
    public static class VfxAuthoringBakeHasher
    {
        /// <summary>Hashes one Effect's Recipe graph, Primitive contracts, overrides, and baker identity.</summary>
        /// <remarks>LowFreqAlloc. Authoring-only canonical string construction and SHA-256.</remarks>
        public static string ComputeRecipeInputHash(VfxAuthoringCatalogDocument catalog, VfxAuthoringEffect effect, string bakerId, string bakerVersion)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            if (string.IsNullOrWhiteSpace(effect.RecipeId))
                return string.Empty;

            var primitives = new Dictionary<string, VfxAuthoringPrimitive>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Primitives.Count; i++)
                if (catalog.Primitives[i] != null && !string.IsNullOrWhiteSpace(catalog.Primitives[i].PrimitiveId))
                    primitives[catalog.Primitives[i].PrimitiveId] = catalog.Primitives[i];
            var recipes = new Dictionary<string, VfxAuthoringRecipe>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Recipes.Count; i++)
                if (catalog.Recipes[i] != null && !string.IsNullOrWhiteSpace(catalog.Recipes[i].RecipeId))
                    recipes[catalog.Recipes[i].RecipeId] = catalog.Recipes[i];
            if (!recipes.TryGetValue(effect.RecipeId, out VfxAuthoringRecipe root))
                return string.Empty;

            var builder = new StringBuilder(1024);
            builder.Append("baker=").Append(bakerId ?? string.Empty).Append('@').Append(bakerVersion ?? string.Empty).Append(';');
            AppendRecipe(builder, root, recipes, primitives, new HashSet<string>(StringComparer.Ordinal));
            builder.Append("effect=").Append(effect.EffectId).Append(';');
            AppendJsonDictionary(builder, effect.ParameterOverrides);
            using SHA256 sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var hex = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                hex.Append(bytes[i].ToString("x2"));
            return "sha256:" + hex;
        }

        private static void AppendRecipe(StringBuilder builder, VfxAuthoringRecipe recipe, Dictionary<string, VfxAuthoringRecipe> recipes, Dictionary<string, VfxAuthoringPrimitive> primitives, HashSet<string> visiting)
        {
            if (!visiting.Add(recipe.RecipeId))
                throw new InvalidOperationException("Recipe dependency cycle prevents bake hashing: " + recipe.RecipeId);
            builder.Append("recipe{").Append(recipe.RecipeId).Append('|');
            for (int i = 0; i < recipe.Parameters.Count; i++)
                builder.Append("p:").Append(recipe.Parameters[i]).Append(';');
            for (int i = 0; i < recipe.IncludedRecipeIds.Count; i++)
                if (recipes.TryGetValue(recipe.IncludedRecipeIds[i], out VfxAuthoringRecipe included))
                    AppendRecipe(builder, included, recipes, primitives, visiting);
            for (int i = 0; i < recipe.Layers.Count; i++)
            {
                VfxAuthoringRecipeLayer layer = recipe.Layers[i];
                if (layer == null)
                    continue;
                builder.Append("layer:").Append(layer.Order).Append(':').Append(layer.LayerId).Append(':').Append(layer.PrimitiveId).Append(';');
                AppendStringDictionary(builder, layer.Bindings);
                AppendJsonDictionary(builder, layer.Overrides);
                if (primitives.TryGetValue(layer.PrimitiveId, out VfxAuthoringPrimitive primitive))
                    AppendPrimitive(builder, primitive);
            }
            builder.Append("};");
            visiting.Remove(recipe.RecipeId);
        }

        private static void AppendPrimitive(StringBuilder builder, VfxAuthoringPrimitive primitive)
        {
            builder.Append("primitive{").Append(primitive.PrimitiveId).Append('|').Append(primitive.Backend).Append('|')
                .Append(primitive.AssetRef != null ? primitive.AssetRef.Kind : string.Empty).Append('|')
                .Append(primitive.AssetRef != null ? primitive.AssetRef.Path : string.Empty).Append('|');
            for (int i = 0; i < primitive.CompatibleAnchors.Count; i++)
                builder.Append("a:").Append(primitive.CompatibleAnchors[i]).Append(';');
            for (int i = 0; i < primitive.Parameters.Count; i++)
            {
                VfxAuthoringParameterDefinition parameter = primitive.Parameters[i];
                if (parameter == null)
                    continue;
                builder.Append("param:").Append(parameter.ParameterId).Append(':').Append(parameter.ValueType).Append(':')
                    .Append(parameter.Unit).Append(':').Append(parameter.Transport).Append(':').Append(parameter.BackendProperty).Append(':')
                    .Append(parameter.RuntimeScale.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
                AppendNullableJson(builder, parameter.DefaultValue);
                AppendNullableJson(builder, parameter.MinValue);
                AppendNullableJson(builder, parameter.MaxValue);
            }
            builder.Append("};");
        }

        private static void AppendStringDictionary(StringBuilder builder, Dictionary<string, string> values)
        {
            if (values == null)
                return;
            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
                builder.Append(keys[i]).Append('=').Append(values[keys[i]]).Append(';');
        }

        private static void AppendJsonDictionary(StringBuilder builder, Dictionary<string, JsonElement> values)
        {
            if (values == null)
                return;
            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                builder.Append(keys[i]).Append('=');
                AppendCanonicalJson(builder, values[keys[i]]);
                builder.Append(';');
            }
        }

        private static void AppendNullableJson(StringBuilder builder, JsonElement? value)
        {
            if (value.HasValue)
                AppendCanonicalJson(builder, value.Value);
            else
                builder.Append("null");
            builder.Append(';');
        }

        private static void AppendCanonicalJson(StringBuilder builder, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (value.TryGetInt64(out long integer)) builder.Append(integer);
                    else builder.Append(value.GetDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case JsonValueKind.String:
                    builder.Append(JsonSerializer.Serialize(value.GetString()));
                    break;
                case JsonValueKind.True: builder.Append("true"); break;
                case JsonValueKind.False: builder.Append("false"); break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined: builder.Append("null"); break;
                case JsonValueKind.Array:
                    builder.Append('[');
                    int index = 0;
                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        if (index++ > 0) builder.Append(',');
                        AppendCanonicalJson(builder, item);
                    }
                    builder.Append(']');
                    break;
                case JsonValueKind.Object:
                    var properties = new List<JsonProperty>();
                    foreach (JsonProperty property in value.EnumerateObject()) properties.Add(property);
                    properties.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
                    builder.Append('{');
                    for (int propertyIndex = 0; propertyIndex < properties.Count; propertyIndex++)
                    {
                        if (propertyIndex > 0) builder.Append(',');
                        builder.Append(JsonSerializer.Serialize(properties[propertyIndex].Name)).Append(':');
                        AppendCanonicalJson(builder, properties[propertyIndex].Value);
                    }
                    builder.Append('}');
                    break;
            }
        }
    }

    public static class VfxAuthoringBakeDiagnosticCodes
    {
        public const string RecipeRequired = "VFXBAKE001";
        public const string PublishedPathRequired = "VFXBAKE002";
        public const string ValidationBlocked = "VFXBAKE003";
        public const string UnityWriteRequired = "VFXBAKE004";
        public const string UpToDate = "VFXBAKE005";
    }

    /// <summary>Dry-run plan for baking one Recipe-backed Published Effect.</summary>
    /// <remarks>LowFreqAlloc. Never writes Unity assets.</remarks>
    public sealed class VfxAuthoringBakePlan
    {
        public string EffectId { get; set; } = string.Empty;
        public string RecipeId { get; set; } = string.Empty;
        public string OutputAssetPath { get; set; } = string.Empty;
        public string ExpectedRecipeInputHash { get; set; } = string.Empty;
        public bool WouldWriteUnityPrefab { get; set; }
        public bool WouldUpdateCatalogEvidence { get; set; }
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
        public bool Blocked => Diagnostics.Exists(item => item.Severity == CharacterAuthoringValidationSeverity.Error);
    }

    public static class VfxAuthoringBakePlanner
    {
        /// <summary>Creates a no-write bake impact plan for one Effect.</summary>
        /// <remarks>LowFreqAlloc. Runs full catalog validation and deterministic hashing.</remarks>
        public static VfxAuthoringBakePlan Plan(VfxAuthoringCatalogDocument catalog, string effectId, VfxAuthoringCatalogEvidence evidence)
        {
            var plan = new VfxAuthoringBakePlan { EffectId = effectId ?? string.Empty };
            VfxAuthoringEffect effect = FindEffect(catalog, effectId);
            if (effect == null || string.IsNullOrWhiteSpace(effect.RecipeId))
            {
                Add(plan, CharacterAuthoringValidationSeverity.Error, VfxAuthoringBakeDiagnosticCodes.RecipeRequired, "Recipe-backed Effect is required for baking.", "Bind an existing Recipe before planning a bake.");
                return plan;
            }
            plan.RecipeId = effect.RecipeId;
            if (effect.Published == null || string.IsNullOrWhiteSpace(effect.Published.AssetPath))
            {
                Add(plan, CharacterAuthoringValidationSeverity.Error, VfxAuthoringBakeDiagnosticCodes.PublishedPathRequired, "Published Effect assetPath is required.", "Set published.assetPath to a project-relative Prefab path.");
                return plan;
            }
            plan.OutputAssetPath = effect.Published.AssetPath;
            VfxAuthoringCatalogValidationReport validation = VfxAuthoringCatalogValidator.Validate(catalog, evidence);
            if (validation.HasErrors)
            {
                Add(plan, CharacterAuthoringValidationSeverity.Error, VfxAuthoringBakeDiagnosticCodes.ValidationBlocked, "Catalog validation blocks Recipe baking.", "Resolve VFXAUTH errors before baking.");
                return plan;
            }
            string bakerId = string.IsNullOrWhiteSpace(effect.Published.BakerId) ? VfxAuthoringBakerIds.ParticleSystemPrefab : effect.Published.BakerId;
            string bakerVersion = string.IsNullOrWhiteSpace(effect.Published.BakerVersion) ? VfxAuthoringBakerIds.ParticleSystemPrefabVersion : effect.Published.BakerVersion;
            plan.ExpectedRecipeInputHash = VfxAuthoringBakeHasher.ComputeRecipeInputHash(catalog, effect, bakerId, bakerVersion);
            string observedHash = string.Empty;
            string observedPath = string.Empty;
            evidence?.PublishedRecipeInputHashes.TryGetValue(effect.EffectId, out observedHash);
            evidence?.PublishedAssetPaths.TryGetValue(effect.EffectId, out observedPath);
            bool upToDate = string.Equals(plan.ExpectedRecipeInputHash, effect.Published.RecipeInputHash, StringComparison.Ordinal) &&
                            string.Equals(plan.ExpectedRecipeInputHash, observedHash, StringComparison.Ordinal) &&
                            string.Equals(effect.Published.AssetPath, observedPath, StringComparison.Ordinal) &&
                            evidence != null && evidence.UnityAssetPaths.Contains(effect.Published.AssetPath);
            plan.WouldWriteUnityPrefab = !upToDate;
            plan.WouldUpdateCatalogEvidence = !string.Equals(plan.ExpectedRecipeInputHash, effect.Published.RecipeInputHash, StringComparison.Ordinal);
            Add(plan, CharacterAuthoringValidationSeverity.Info, upToDate ? VfxAuthoringBakeDiagnosticCodes.UpToDate : VfxAuthoringBakeDiagnosticCodes.UnityWriteRequired,
                upToDate ? "Published Effect is up to date." : "Unity Editor bake is required; no asset was written by this plan.",
                upToDate ? "No action required." : "Run the formal Unity VFX Recipe baker, review the Prefab, then update catalog bake evidence.");
            return plan;
        }

        private static VfxAuthoringEffect FindEffect(VfxAuthoringCatalogDocument catalog, string effectId)
        {
            if (catalog == null || catalog.Effects == null)
                return null;
            for (int i = 0; i < catalog.Effects.Count; i++)
                if (catalog.Effects[i] != null && string.Equals(catalog.Effects[i].EffectId, effectId, StringComparison.Ordinal))
                    return catalog.Effects[i];
            return null;
        }

        private static void Add(VfxAuthoringBakePlan plan, CharacterAuthoringValidationSeverity severity, string code, string message, string fix)
        {
            plan.Diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(severity, code, plan.EffectId, string.Empty, message, fix, "bake"));
        }
    }
}
