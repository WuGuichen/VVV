using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class VfxAuthoringSearchQuery
    {
        public string Text { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public string Anchor { get; set; } = string.Empty;
        public string Backend { get; set; } = string.Empty;
        public string ReviewStatus { get; set; } = string.Empty;
    }

    public sealed class VfxAuthoringSearchResult
    {
        public string StableId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public int Score { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Backend { get; set; } = string.Empty;
        public string ReviewStatus { get; set; } = string.Empty;
        public string VisualMaturity { get; set; } = VfxAuthoringVisualMaturities.LegacyPlaceholder;
        public string ResourceKey { get; set; } = string.Empty;
        public string RecipeId { get; set; } = string.Empty;
        public List<string> Reasons { get; set; } = new List<string>();
    }

    public sealed class VfxAuthoringSearchReport
    {
        public string SchemaVersion { get; set; } = "mx.vfx.authoring.search.v1";
        public VfxAuthoringSearchQuery Query { get; set; } = new VfxAuthoringSearchQuery();
        public string Recommendation { get; set; } = string.Empty;
        public List<VfxAuthoringSearchResult> Results { get; set; } = new List<VfxAuthoringSearchResult>();
    }

    public sealed class VfxAuthoringImpactReport
    {
        public string SchemaVersion { get; set; } = "mx.vfx.authoring.impact.v1";
        public string StableId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public bool Found { get; set; }
        public bool SafeToDelete { get; set; }
        public List<string> ReferencedByRecipes { get; set; } = new List<string>();
        public List<string> ReferencedByEffects { get; set; } = new List<string>();
        public List<string> ReferencedAsFallbackByEffects { get; set; } = new List<string>();
        public List<string> ResourceAndDefinitionBindings { get; set; } = new List<string>();
    }

    /// <summary>Read-only semantic search, reuse recommendation, and impact analysis for Development Agent workflows.</summary>
    public static class VfxAuthoringSearchService
    {
        /// <summary>Searches Effect, Recipe, then Primitive candidates with deterministic scoring.</summary>
        /// <remarks>LowFreqAlloc. Authoring-only query; never mutates review state or assets.</remarks>
        public static VfxAuthoringSearchReport Search(VfxAuthoringCatalogDocument catalog, VfxAuthoringSearchQuery query, int limit = 20)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            query ??= new VfxAuthoringSearchQuery();
            var report = new VfxAuthoringSearchReport { Query = query };
            AddEffects(catalog, query, report.Results);
            AddRecipes(catalog, query, report.Results);
            AddPrimitives(catalog, query, report.Results);
            report.Results.Sort(CompareResults);
            if (limit > 0 && report.Results.Count > limit)
                report.Results.RemoveRange(limit, report.Results.Count - limit);
            report.Recommendation = BuildRecommendation(report.Results);
            return report;
        }

        /// <summary>Reports semantic and runtime-binding consumers before replacement, merge, or deletion.</summary>
        /// <remarks>LowFreqAlloc. Read-only deterministic dependency traversal.</remarks>
        public static VfxAuthoringImpactReport AnalyzeImpact(VfxAuthoringCatalogDocument catalog, string stableId)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            var report = new VfxAuthoringImpactReport { StableId = stableId ?? string.Empty };
            for (int i = 0; i < catalog.Primitives.Count; i++)
                if (catalog.Primitives[i] != null && string.Equals(catalog.Primitives[i].PrimitiveId, stableId, StringComparison.Ordinal))
                {
                    report.Found = true;
                    report.Kind = "Primitive";
                }
            for (int i = 0; i < catalog.Recipes.Count; i++)
            {
                VfxAuthoringRecipe recipe = catalog.Recipes[i];
                if (recipe == null)
                    continue;
                if (string.Equals(recipe.RecipeId, stableId, StringComparison.Ordinal))
                {
                    report.Found = true;
                    report.Kind = "Recipe";
                }
                if (Contains(recipe.IncludedRecipeIds, stableId) || UsesPrimitive(recipe, stableId))
                    report.ReferencedByRecipes.Add(recipe.RecipeId);
            }
            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect effect = catalog.Effects[i];
                if (effect == null)
                    continue;
                if (string.Equals(effect.EffectId, stableId, StringComparison.Ordinal))
                {
                    report.Found = true;
                    report.Kind = "Effect";
                    report.ResourceAndDefinitionBindings.Add(effect.ResourceKey.ToStableKey() + "#definition=" + effect.DefinitionId);
                }
                if (string.Equals(effect.RecipeId, stableId, StringComparison.Ordinal))
                    report.ReferencedByEffects.Add(effect.EffectId);
                if (effect.Quality != null && string.Equals(effect.Quality.FallbackEffectId, stableId, StringComparison.Ordinal))
                    report.ReferencedAsFallbackByEffects.Add(effect.EffectId);
            }
            report.ReferencedByRecipes.Sort(StringComparer.Ordinal);
            report.ReferencedByEffects.Sort(StringComparer.Ordinal);
            report.ReferencedAsFallbackByEffects.Sort(StringComparer.Ordinal);
            report.ResourceAndDefinitionBindings.Sort(StringComparer.Ordinal);
            report.SafeToDelete = report.Found && report.ReferencedByRecipes.Count == 0 && report.ReferencedByEffects.Count == 0 &&
                                  report.ReferencedAsFallbackByEffects.Count == 0 && report.ResourceAndDefinitionBindings.Count == 0;
            return report;
        }

        private static void AddEffects(VfxAuthoringCatalogDocument catalog, VfxAuthoringSearchQuery query, List<VfxAuthoringSearchResult> output)
        {
            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect effect = catalog.Effects[i];
                if (effect == null)
                    continue;
                var result = new VfxAuthoringSearchResult
                {
                    StableId = effect.EffectId,
                    Kind = "Effect",
                    DisplayName = effect.DisplayName,
                    Backend = effect.Backend,
                    ReviewStatus = effect.ReviewStatus,
                    VisualMaturity = effect.VisualMaturity,
                    ResourceKey = effect.ResourceKey.ToStableKey(),
                    RecipeId = effect.RecipeId
                };
                ScoreCommon(query, effect.EffectId, effect.DisplayName, effect.Description, effect.Tags, effect.Roles, effect.Backend, effect.ReviewStatus, result);
                ScoreExact(query.Theme, effect.ThemeIds, 30, "theme", result);
                if (!string.IsNullOrWhiteSpace(query.Anchor) && effect.AnchorContract != null && string.Equals(query.Anchor, effect.AnchorContract.Anchor, StringComparison.OrdinalIgnoreCase)) AddScore(result, 25, "anchor");
                if (MatchesRequired(query, result, effect.Roles, effect.ThemeIds, effect.AnchorContract?.Anchor)) output.Add(result);
            }
        }

        private static void AddRecipes(VfxAuthoringCatalogDocument catalog, VfxAuthoringSearchQuery query, List<VfxAuthoringSearchResult> output)
        {
            for (int i = 0; i < catalog.Recipes.Count; i++)
            {
                VfxAuthoringRecipe recipe = catalog.Recipes[i];
                if (recipe == null)
                    continue;
                var result = new VfxAuthoringSearchResult { StableId = recipe.RecipeId, Kind = "Recipe", DisplayName = recipe.DisplayName, ReviewStatus = recipe.ReviewStatus, VisualMaturity = recipe.VisualMaturity };
                ScoreText(query.Text, recipe.RecipeId, recipe.DisplayName, recipe.Description, recipe.Tags, result);
                if (string.IsNullOrWhiteSpace(query.Role) && string.IsNullOrWhiteSpace(query.Theme) && string.IsNullOrWhiteSpace(query.Anchor) && string.IsNullOrWhiteSpace(query.Backend) &&
                    (string.IsNullOrWhiteSpace(query.ReviewStatus) || string.Equals(query.ReviewStatus, recipe.ReviewStatus, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(query.Text) || result.Score > 0)) output.Add(result);
            }
        }

        private static void AddPrimitives(VfxAuthoringCatalogDocument catalog, VfxAuthoringSearchQuery query, List<VfxAuthoringSearchResult> output)
        {
            for (int i = 0; i < catalog.Primitives.Count; i++)
            {
                VfxAuthoringPrimitive primitive = catalog.Primitives[i];
                if (primitive == null)
                    continue;
                var result = new VfxAuthoringSearchResult { StableId = primitive.PrimitiveId, Kind = "Primitive", DisplayName = primitive.DisplayName, Backend = primitive.Backend, ReviewStatus = primitive.ReviewStatus, VisualMaturity = primitive.VisualMaturity };
                ScoreCommon(query, primitive.PrimitiveId, primitive.DisplayName, primitive.Description, primitive.Tags, primitive.Roles, primitive.Backend, primitive.ReviewStatus, result);
                if (!string.IsNullOrWhiteSpace(query.Anchor) && ContainsIgnoreCase(primitive.CompatibleAnchors, query.Anchor)) AddScore(result, 25, "anchor-compatible");
                if (MatchesRequired(query, result, primitive.Roles, null, ContainsIgnoreCase(primitive.CompatibleAnchors, query.Anchor) ? query.Anchor : string.Empty)) output.Add(result);
            }
        }

        private static void ScoreCommon(VfxAuthoringSearchQuery query, string id, string name, string description, List<string> tags, List<string> roles, string backend, string review, VfxAuthoringSearchResult result)
        {
            ScoreText(query.Text, id, name, description, tags, result);
            ScoreExact(query.Role, roles, 40, "role", result);
            if (!string.IsNullOrWhiteSpace(query.Backend) && string.Equals(query.Backend, backend, StringComparison.OrdinalIgnoreCase)) AddScore(result, 20, "backend");
            if (!string.IsNullOrWhiteSpace(query.ReviewStatus) && string.Equals(query.ReviewStatus, review, StringComparison.OrdinalIgnoreCase)) AddScore(result, 10, "review");
        }

        private static void ScoreText(string text, string id, string name, string description, List<string> tags, VfxAuthoringSearchResult result)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (ContainsIgnoreCase(id, text)) AddScore(result, 50, "id/text");
            if (ContainsIgnoreCase(name, text)) AddScore(result, 35, "name/text");
            if (ContainsIgnoreCase(description, text)) AddScore(result, 15, "description/text");
            if (ContainsIgnoreCase(tags, text)) AddScore(result, 25, "tag/text");
        }

        private static void ScoreExact(string expected, List<string> values, int score, string reason, VfxAuthoringSearchResult result)
        {
            if (!string.IsNullOrWhiteSpace(expected) && ContainsIgnoreCase(values, expected)) AddScore(result, score, reason);
        }

        private static bool MatchesRequired(VfxAuthoringSearchQuery query, VfxAuthoringSearchResult result, List<string> roles, List<string> themes, string anchor)
        {
            if (!string.IsNullOrWhiteSpace(query.Text) && result.Score == 0) return false;
            if (!string.IsNullOrWhiteSpace(query.Role) && !ContainsIgnoreCase(roles, query.Role)) return false;
            if (!string.IsNullOrWhiteSpace(query.Theme) && !ContainsIgnoreCase(themes, query.Theme)) return false;
            if (!string.IsNullOrWhiteSpace(query.Anchor) && !string.Equals(query.Anchor, anchor, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(query.Backend) && !string.Equals(query.Backend, result.Backend, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrWhiteSpace(query.ReviewStatus) && !string.Equals(query.ReviewStatus, result.ReviewStatus, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static string BuildRecommendation(List<VfxAuthoringSearchResult> results)
        {
            for (int i = 0; i < results.Count; i++) if (IsProduction(results[i]) && string.Equals(results[i].Kind, "Effect", StringComparison.Ordinal)) return "Reuse or parameterize the highest-ranked Production Effect before creating a new asset; human review is required for replacement.";
            for (int i = 0; i < results.Count; i++) if (IsProduction(results[i]) && string.Equals(results[i].Kind, "Recipe", StringComparison.Ordinal)) return "Reuse the highest-ranked Production Recipe and bake a new Published Effect before creating a Primitive.";
            for (int i = 0; i < results.Count; i++) if (IsProduction(results[i]) && string.Equals(results[i].Kind, "Primitive", StringComparison.Ordinal)) return "Compose a Recipe from the highest-ranked Production Primitive before proposing a new Primitive.";
            if (results.Count > 0) return "Only LegacyPlaceholder or Candidate matches exist; use them as contract/review evidence, not as visual reuse inputs. Create an original candidate for the documented gap.";
            return "No semantic candidate matched; inspect reference evidence and document why a new Primitive is required.";
        }

        private static int CompareResults(VfxAuthoringSearchResult left, VfxAuthoringSearchResult right)
        {
            int maturity = MaturityOrder(left.VisualMaturity).CompareTo(MaturityOrder(right.VisualMaturity));
            if (maturity != 0) return maturity;
            int score = right.Score.CompareTo(left.Score);
            if (score != 0) return score;
            int kind = KindOrder(left.Kind).CompareTo(KindOrder(right.Kind));
            return kind != 0 ? kind : string.Compare(left.StableId, right.StableId, StringComparison.Ordinal);
        }

        private static int KindOrder(string kind) => string.Equals(kind, "Effect", StringComparison.Ordinal) ? 0 : string.Equals(kind, "Recipe", StringComparison.Ordinal) ? 1 : 2;
        private static int MaturityOrder(string maturity) => string.Equals(maturity, VfxAuthoringVisualMaturities.Production, StringComparison.Ordinal) ? 0 : string.Equals(maturity, VfxAuthoringVisualMaturities.Candidate, StringComparison.Ordinal) ? 1 : 2;
        private static bool IsProduction(VfxAuthoringSearchResult result) => result != null && string.Equals(result.VisualMaturity, VfxAuthoringVisualMaturities.Production, StringComparison.Ordinal);
        private static void AddScore(VfxAuthoringSearchResult result, int score, string reason) { result.Score += score; result.Reasons.Add(reason); }
        private static bool UsesPrimitive(VfxAuthoringRecipe recipe, string id) { for (int i = 0; i < recipe.Layers.Count; i++) if (recipe.Layers[i] != null && string.Equals(recipe.Layers[i].PrimitiveId, id, StringComparison.Ordinal)) return true; return false; }
        private static bool Contains(List<string> values, string expected) { if (values == null) return false; for (int i = 0; i < values.Count; i++) if (string.Equals(values[i], expected, StringComparison.Ordinal)) return true; return false; }
        private static bool ContainsIgnoreCase(List<string> values, string expected) { if (values == null || string.IsNullOrWhiteSpace(expected)) return false; for (int i = 0; i < values.Count; i++) if (ContainsIgnoreCase(values[i], expected)) return true; return false; }
        private static bool ContainsIgnoreCase(string value, string expected) => !string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(expected) && value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
