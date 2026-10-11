using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>Builds reuse-first decisions from human-Ready VFX entries.</summary>
    public static class VfxReusePlanner
    {
        /// <summary>Searches Ready Effect, then Ready Recipe/Primitive composition, before requesting a new Primitive.</summary>
        /// <remarks>AllocByDesign. Builds compatibility candidates and stable rationale; performs no writes.</remarks>
        public static VfxReuseDecision Plan(VfxAuthoringCatalogDocument catalog, VfxCreativeBrief brief, string actualCatalogHash)
        {
            var decision = new VfxReuseDecision
            {
                BriefId = brief != null ? brief.BriefId ?? string.Empty : string.Empty,
                CatalogHash = actualCatalogHash ?? string.Empty
            };
            if (catalog == null || brief == null || !string.Equals(brief.SchemaVersion, VfxCreativeFlywheelSchemaVersions.CreativeBriefV1, StringComparison.Ordinal))
            {
                Add(decision.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, decision.BriefId, "Creative Brief or VFX Catalog is missing/incompatible.", "Provide a supported Creative Brief and Catalog.", "reuse");
                Finish(decision);
                return decision;
            }
            NormalizeBrief(brief);
            if (!IsStableId(brief.BriefId) || brief.Roles.Count == 0 || string.IsNullOrWhiteSpace(brief.QualityProfileId))
                Add(decision.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, brief.BriefId, "Creative Brief requires stable id, role, and quality profile.", "Complete the Brief before reuse planning.", "brief");
            if (!string.Equals(brief.CatalogHash, actualCatalogHash, StringComparison.OrdinalIgnoreCase))
                Add(decision.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.CatalogStale, brief.BriefId, "Creative Brief Catalog hash is stale.", "Reload the current Catalog and recreate the Brief.", "brief/catalogHash");

            var primitives = IndexPrimitives(catalog);
            var recipes = IndexRecipes(catalog);
            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect effect = catalog.Effects[i];
                if (effect == null || !IsProductionReady(effect.VisualMaturity, effect.ReviewStatus))
                    continue;
                int score = ScoreEffect(effect, brief, recipes);
                var candidate = new VfxReuseCandidate
                {
                    StableId = effect.EffectId,
                    Kind = VfxPromotionTargetKinds.Effect,
                    CompatibilityScore = score
                };
                candidate.Reasons.Add(effect.Exemplar ? "Ready exemplar" : "Ready Published Effect");
                if (ContainsAll(effect.Roles, brief.Roles)) candidate.Reasons.Add("role match");
                if (ContainsAll(effect.ThemeIds, brief.ThemeIds)) candidate.Reasons.Add("theme match");
                decision.Candidates.Add(candidate);
            }
            decision.Candidates.Sort(CompareCandidates);

            for (int i = 0; i < decision.Candidates.Count; i++)
            {
                VfxReuseCandidate candidate = decision.Candidates[i];
                if (candidate.CompatibilityScore < 100 || !string.Equals(candidate.Kind, VfxPromotionTargetKinds.Effect, StringComparison.Ordinal))
                    continue;
                decision.DecisionKind = VfxReuseDecisionKinds.ReusePublishedEffect;
                decision.SelectedEffectId = candidate.StableId;
                Finish(decision);
                return decision;
            }

            var recipeCandidates = new List<VfxReuseCandidate>();
            for (int i = 0; i < catalog.Recipes.Count; i++)
            {
                VfxAuthoringRecipe recipe = catalog.Recipes[i];
                if (recipe == null || !IsReadyRecipeClosure(recipe, recipes, primitives, new HashSet<string>(StringComparer.Ordinal)))
                    continue;
                int score = ScoreRecipe(recipe, brief, recipes, primitives);
                var candidate = new VfxReuseCandidate
                {
                    StableId = recipe.RecipeId,
                    Kind = VfxPromotionTargetKinds.Recipe,
                    CompatibilityScore = score
                };
                candidate.Reasons.Add(recipe.Exemplar ? "Ready canonical Recipe" : "Ready Recipe");
                recipeCandidates.Add(candidate);
                decision.Candidates.Add(candidate);
            }
            recipeCandidates.Sort(CompareCandidates);
            decision.Candidates.Sort(CompareCandidates);
            if (recipeCandidates.Count > 0 && recipeCandidates[0].CompatibilityScore >= 85)
            {
                decision.DecisionKind = VfxReuseDecisionKinds.ComposeReadyRecipe;
                decision.SelectedRecipeIds.Add(recipeCandidates[0].StableId);
                if (recipes.TryGetValue(recipeCandidates[0].StableId, out VfxAuthoringRecipe selectedRecipe))
                    CollectPrimitiveIds(selectedRecipe, recipes, decision.SelectedPrimitiveIds, new HashSet<string>(StringComparer.Ordinal));
                Finish(decision);
                return decision;
            }

            decision.DecisionKind = VfxReuseDecisionKinds.GeneratePrimitive;
            for (int i = 0; i < catalog.Primitives.Count; i++)
            {
                VfxAuthoringPrimitive primitive = catalog.Primitives[i];
                if (primitive == null || !IsProductionReady(primitive.VisualMaturity, primitive.ReviewStatus))
                    continue;
                int score = ScorePrimitive(primitive, brief);
                if (score >= 50)
                    decision.SelectedPrimitiveIds.Add(primitive.PrimitiveId);
            }
            decision.SelectedPrimitiveIds.Sort(StringComparer.Ordinal);
            decision.PrimitiveBriefs.Add("Generate the missing single-responsibility Primitive for roles=" + string.Join(",", brief.Roles) + ", backend=" + brief.PreferredBackend + ", anchor=" + brief.AnchorContract.Anchor + ".");
            Finish(decision);
            return decision;
        }

        private static int ScoreEffect(VfxAuthoringEffect effect, VfxCreativeBrief brief, Dictionary<string, VfxAuthoringRecipe> recipes)
        {
            if (!ContainsAll(effect.Roles, brief.Roles) || !ContainsAll(effect.ThemeIds, brief.ThemeIds) ||
                !string.Equals(effect.Backend, brief.PreferredBackend, StringComparison.Ordinal) || effect.AnchorContract == null || brief.AnchorContract == null ||
                !string.Equals(effect.AnchorContract.Anchor, brief.AnchorContract.Anchor, StringComparison.Ordinal) ||
                !string.Equals(effect.AnchorContract.Space, brief.AnchorContract.Space, StringComparison.Ordinal) ||
                effect.Lifecycle == null || brief.Lifecycle == null || !string.Equals(effect.Lifecycle.Mode, brief.Lifecycle.Mode, StringComparison.Ordinal) ||
                TierRank(effect.Quality != null ? effect.Quality.CostTier : VfxAuthoringCostTiers.High) > TierRank(brief.TargetCostTier) ||
                !EffectHasParameters(effect, brief.RequiredParameterIds, recipes))
                return 0;
            int score = 100;
            if (effect.Exemplar) score += 10;
            return score;
        }

        private static int ScoreRecipe(VfxAuthoringRecipe recipe, VfxCreativeBrief brief, Dictionary<string, VfxAuthoringRecipe> recipes, Dictionary<string, VfxAuthoringPrimitive> primitives)
        {
            var used = new List<VfxAuthoringPrimitive>();
            CollectPrimitives(recipe, recipes, primitives, used, new HashSet<string>(StringComparer.Ordinal));
            if (used.Count == 0 || !ContainsAll(recipe.Parameters, brief.RequiredParameterIds)) return 0;
            var roles = new List<string>();
            for (int i = 0; i < used.Count; i++)
            {
                if (!string.Equals(used[i].Backend, brief.PreferredBackend, StringComparison.Ordinal) || !used[i].CompatibleAnchors.Contains(brief.AnchorContract.Anchor)) return 0;
                for (int roleIndex = 0; roleIndex < used[i].Roles.Count; roleIndex++) if (!roles.Contains(used[i].Roles[roleIndex])) roles.Add(used[i].Roles[roleIndex]);
            }
            if (!ContainsAll(roles, brief.Roles)) return 0;
            return 85 + (recipe.Exemplar ? 10 : 0);
        }

        private static int ScorePrimitive(VfxAuthoringPrimitive primitive, VfxCreativeBrief brief)
        {
            int score = 0;
            if (string.Equals(primitive.Backend, brief.PreferredBackend, StringComparison.Ordinal)) score += 25;
            if (primitive.CompatibleAnchors.Contains(brief.AnchorContract.Anchor)) score += 25;
            for (int i = 0; i < brief.Roles.Count; i++) if (primitive.Roles.Contains(brief.Roles[i])) score += 20;
            if (ContainsAll(ParameterIds(primitive), brief.RequiredParameterIds)) score += 20;
            if (primitive.Exemplar) score += 10;
            return score;
        }

        private static bool EffectHasParameters(VfxAuthoringEffect effect, List<string> required, Dictionary<string, VfxAuthoringRecipe> recipes)
        {
            if (required.Count == 0) return true;
            return !string.IsNullOrWhiteSpace(effect.RecipeId) && recipes.TryGetValue(effect.RecipeId, out VfxAuthoringRecipe recipe) && ContainsAll(recipe.Parameters, required);
        }

        private static bool IsReadyRecipeClosure(VfxAuthoringRecipe recipe, Dictionary<string, VfxAuthoringRecipe> recipes, Dictionary<string, VfxAuthoringPrimitive> primitives, HashSet<string> visited)
        {
            if (!visited.Add(recipe.RecipeId) || !IsProductionReady(recipe.VisualMaturity, recipe.ReviewStatus)) return false;
            for (int i = 0; i < recipe.Layers.Count; i++)
                if (recipe.Layers[i] == null || !primitives.TryGetValue(recipe.Layers[i].PrimitiveId, out VfxAuthoringPrimitive primitive) || !IsProductionReady(primitive.VisualMaturity, primitive.ReviewStatus)) return false;
            for (int i = 0; i < recipe.IncludedRecipeIds.Count; i++)
                if (!recipes.TryGetValue(recipe.IncludedRecipeIds[i], out VfxAuthoringRecipe included) || !IsReadyRecipeClosure(included, recipes, primitives, visited)) return false;
            return true;
        }

        private static bool IsProductionReady(string maturity, string reviewStatus) =>
            string.Equals(maturity, VfxAuthoringVisualMaturities.Production, StringComparison.Ordinal) &&
            string.Equals(reviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal);

        private static void CollectPrimitives(VfxAuthoringRecipe recipe, Dictionary<string, VfxAuthoringRecipe> recipes, Dictionary<string, VfxAuthoringPrimitive> primitives, List<VfxAuthoringPrimitive> output, HashSet<string> visited)
        {
            if (!visited.Add(recipe.RecipeId)) return;
            for (int i = 0; i < recipe.Layers.Count; i++)
                if (recipe.Layers[i] != null && primitives.TryGetValue(recipe.Layers[i].PrimitiveId, out VfxAuthoringPrimitive primitive) && !output.Exists(item => string.Equals(item.PrimitiveId, primitive.PrimitiveId, StringComparison.Ordinal))) output.Add(primitive);
            for (int i = 0; i < recipe.IncludedRecipeIds.Count; i++) if (recipes.TryGetValue(recipe.IncludedRecipeIds[i], out VfxAuthoringRecipe included)) CollectPrimitives(included, recipes, primitives, output, visited);
        }

        private static void CollectPrimitiveIds(VfxAuthoringRecipe recipe, Dictionary<string, VfxAuthoringRecipe> recipes, List<string> output, HashSet<string> visited)
        {
            if (!visited.Add(recipe.RecipeId)) return;
            for (int i = 0; i < recipe.Layers.Count; i++) if (recipe.Layers[i] != null && !output.Contains(recipe.Layers[i].PrimitiveId)) output.Add(recipe.Layers[i].PrimitiveId);
            for (int i = 0; i < recipe.IncludedRecipeIds.Count; i++) if (recipes.TryGetValue(recipe.IncludedRecipeIds[i], out VfxAuthoringRecipe included)) CollectPrimitiveIds(included, recipes, output, visited);
            output.Sort(StringComparer.Ordinal);
        }

        private static Dictionary<string, VfxAuthoringPrimitive> IndexPrimitives(VfxAuthoringCatalogDocument catalog)
        {
            var result = new Dictionary<string, VfxAuthoringPrimitive>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Primitives.Count; i++) if (catalog.Primitives[i] != null && !string.IsNullOrWhiteSpace(catalog.Primitives[i].PrimitiveId)) result[catalog.Primitives[i].PrimitiveId] = catalog.Primitives[i];
            return result;
        }

        private static Dictionary<string, VfxAuthoringRecipe> IndexRecipes(VfxAuthoringCatalogDocument catalog)
        {
            var result = new Dictionary<string, VfxAuthoringRecipe>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Recipes.Count; i++) if (catalog.Recipes[i] != null && !string.IsNullOrWhiteSpace(catalog.Recipes[i].RecipeId)) result[catalog.Recipes[i].RecipeId] = catalog.Recipes[i];
            return result;
        }

        private static List<string> ParameterIds(VfxAuthoringPrimitive primitive)
        {
            var result = new List<string>();
            for (int i = 0; i < primitive.Parameters.Count; i++) if (primitive.Parameters[i] != null) result.Add(primitive.Parameters[i].ParameterId);
            return result;
        }

        private static int TierRank(string value) => string.Equals(value, VfxAuthoringCostTiers.Low, StringComparison.Ordinal) ? 0 : string.Equals(value, VfxAuthoringCostTiers.Medium, StringComparison.Ordinal) ? 1 : 2;

        private static bool ContainsAll(List<string> values, List<string> expected)
        {
            for (int i = 0; i < expected.Count; i++) if (!values.Contains(expected[i])) return false;
            return true;
        }

        private static int CompareCandidates(VfxReuseCandidate left, VfxReuseCandidate right)
        {
            int score = right.CompatibilityScore.CompareTo(left.CompatibilityScore);
            return score != 0 ? score : string.Compare(left.StableId, right.StableId, StringComparison.Ordinal);
        }

        internal static bool IsStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')) return false;
            }
            return true;
        }

        private static void NormalizeBrief(VfxCreativeBrief brief)
        {
            brief.BriefId = (brief.BriefId ?? string.Empty).Trim();
            brief.CatalogHash = (brief.CatalogHash ?? string.Empty).Trim();
            brief.GenerationTemplateCatalogHash = (brief.GenerationTemplateCatalogHash ?? string.Empty).Trim();
            brief.StyleProfileId = (brief.StyleProfileId ?? string.Empty).Trim();
            brief.QualityProfileId = (brief.QualityProfileId ?? string.Empty).Trim();
            brief.QualityProfileVersion = (brief.QualityProfileVersion ?? string.Empty).Trim();
            brief.Roles ??= new List<string>();
            brief.ThemeIds ??= new List<string>();
            brief.RequiredParameterIds ??= new List<string>();
            brief.ReferenceEffectIds ??= new List<string>();
            brief.ParentCandidateIds ??= new List<string>();
            brief.FixedSeeds ??= new List<int>();
            brief.AnchorContract ??= new VfxAuthoringAnchorContract();
            brief.Lifecycle ??= new VfxAuthoringLifecycle();
            brief.ThemeIds.Sort(StringComparer.Ordinal);
            brief.RequiredParameterIds.Sort(StringComparer.Ordinal);
            brief.ReferenceEffectIds.Sort(StringComparer.Ordinal);
            brief.ParentCandidateIds.Sort(StringComparer.Ordinal);
            brief.FixedSeeds.Sort();
        }

        private static void Finish(VfxReuseDecision decision)
        {
            decision.Diagnostics.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));
            decision.DecisionHash = VfxCreativeFlywheelHash.ComputeReuseDecisionHash(decision);
        }

        internal static void Add(List<AuthoringResourceDiagnostic> diagnostics, CharacterAuthoringValidationSeverity severity, string code, string stableId, string message, string fix, string field)
        {
            diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(severity, code, stableId, string.Empty, message, fix, field));
        }
    }

    /// <summary>Builds deterministic six-way candidate generation plans from a reuse decision.</summary>
    public static class VfxGenerationPlanner
    {
        /// <summary>Creates a read-only candidate plan under the physical Unity-importable sandbox.</summary>
        /// <remarks>AllocByDesign. Selects versioned data and hashes the plan; performs no file or Unity asset writes.</remarks>
        public static VfxGenerationPlan Plan(VfxCreativeBrief brief, VfxReuseDecision reuseDecision, VfxAuthoringCatalogDocument catalog, VfxGenerationTemplateCatalogDocument templates, string actualCatalogHash, string actualTemplateCatalogHash, VfxGenerationSessionCapabilities capabilities = null)
        {
            var plan = new VfxGenerationPlan
            {
                CatalogHash = actualCatalogHash ?? string.Empty,
                GenerationTemplateCatalogHash = actualTemplateCatalogHash ?? string.Empty,
                TargetCostTier = brief != null ? brief.TargetCostTier ?? string.Empty : string.Empty
            };
            if (brief == null || reuseDecision == null || catalog == null || templates == null)
            {
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Generation planning inputs are incomplete.", "Provide Brief, reuse decision, semantic Catalog, and generation template Catalog.", "generationPlan");
                Finish(plan);
                return plan;
            }
            VfxGenerationTemplateCatalog.NormalizeInPlace(templates);
            NormalizeCapabilities(capabilities);
            VfxGenerationTemplateCatalogValidationReport templateValidation = VfxGenerationTemplateCatalog.Validate(templates, catalog);
            plan.Diagnostics.AddRange(templateValidation.Diagnostics);
            ValidateCapabilities(capabilities, templates, plan);
            plan.SessionCapabilitiesHash = capabilities != null ? VfxCreativeFlywheelHash.Compute(capabilities) : string.Empty;
            string briefHash = VfxCreativeFlywheelHash.ComputeBriefHash(brief);
            string decisionHash = VfxCreativeFlywheelHash.ComputeReuseDecisionHash(reuseDecision);
            plan.BriefHash = briefHash;
            plan.ReuseDecisionHash = decisionHash;
            plan.PlanId = "vfx.plan." + NormalizeId(brief.BriefId) + ".r" + brief.Round;
            plan.SessionId = NormalizeSessionId(brief.BriefId) + "-" + ShortHash(briefHash);
            plan.Round = brief.Round;
            plan.QualityProfileId = brief.QualityProfileId;
            plan.QualityProfileVersion = brief.QualityProfileVersion;
            plan.TargetCostTier = brief.TargetCostTier;
            if (!string.Equals(brief.CatalogHash, actualCatalogHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(reuseDecision.CatalogHash, actualCatalogHash, StringComparison.OrdinalIgnoreCase))
                Add(plan, VfxFlywheelDiagnosticCodes.CatalogStale, "Catalog changed after Brief/reuse planning.", "Reload Catalog and recreate the plan.", "catalogHash");
            if (!string.Equals(brief.GenerationTemplateCatalogHash, actualTemplateCatalogHash, StringComparison.OrdinalIgnoreCase))
                Add(plan, VfxFlywheelDiagnosticCodes.TemplateCatalogStale, "Generation template Catalog changed after the Brief was created.", "Reload generation templates and recreate the Brief.", "generationTemplateCatalogHash");
            if (!string.Equals(reuseDecision.DecisionHash, decisionHash, StringComparison.OrdinalIgnoreCase))
                Add(plan, VfxFlywheelDiagnosticCodes.PlanTampered, "Reuse decision hash does not match its content.", "Regenerate the reuse decision.", "reuseDecisionHash");
            if (reuseDecision.Blocked)
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Reuse decision is blocked.", "Resolve reuse diagnostics before generation planning.", "reuseDecision");

            VfxQualityProfile quality = FindQualityProfile(templates, brief.QualityProfileId, brief.QualityProfileVersion);
            if (quality == null)
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Requested quality profile id/version is unavailable.", "Select a versioned profile from generation templates.", "qualityProfile");
            else
            {
                plan.FixedSeeds.AddRange(quality.FixedSeeds);
                plan.QualityProfileVersion = quality.Version;
            }

            if (string.Equals(reuseDecision.DecisionKind, VfxReuseDecisionKinds.ReusePublishedEffect, StringComparison.Ordinal))
            {
                plan.GenerationMode = VfxGenerationModes.ReuseOnly;
                Finish(plan);
                return plan;
            }

            VfxGenerationProviderDefinition provider = SelectProvider(templates, brief.PreferredProviderId, capabilities, out VfxGenerationProviderCapabilityGrant providerGrant);
            VfxGenerationTemplateDefinition template = SelectTemplate(templates, brief, provider, capabilities);
            if (provider == null)
                Add(plan, VfxFlywheelDiagnosticCodes.ProviderUnavailable, string.IsNullOrWhiteSpace(brief.PreferredProviderId) ? "No enabled local-first provider matches the Brief." : "Explicitly requested provider is unknown, disabled, lacks a pinned model, or is not allowed by the external session capability: " + brief.PreferredProviderId, "Select a default-enabled provider or supply a repository-external capability that allowlists the exact configured provider; cloud additionally requires allowCloud=true.", "provider");
            if (template == null)
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "No compatible certified template or allowed experimental adapter matches the Brief.", "Add a certified template with role/backend/Anchor contracts or explicitly enable the version-locked experiment.", "template");
            if (!IsCostTier(brief.TargetCostTier))
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Brief target cost tier is invalid: " + brief.TargetCostTier, "Use Low, Medium, or High.", "brief/targetCostTier");
            else if (template != null && (TierRank(brief.TargetCostTier) < TierRank(template.BudgetRange.MinimumCostTier) || TierRank(brief.TargetCostTier) > TierRank(template.BudgetRange.MaximumCostTier)))
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Brief target cost tier is outside the selected template budget range.", "Select a compatible template or cost tier.", "brief/targetCostTier");
            if (plan.Blocked)
            {
                Finish(plan);
                return plan;
            }

            bool experimental = template.ArbitraryTopology;
            plan.GenerationMode = experimental ? VfxGenerationModes.ExperimentalGraph : VfxGenerationModes.ControlledTemplate;
            plan.SandboxRoot = VfxCandidatePathPolicy.BuildSandboxRoot(plan.SessionId);
            int count = brief.Round <= 0 ? 6 : 4;
            if (brief.Round > 0 && brief.ParentCandidateIds.Count != 2)
                Add(plan, VfxFlywheelDiagnosticCodes.SchemaInvalid, "Evolution rounds require exactly two selected parent candidates.", "Select two Pareto-frontier parents before creating four children.", "brief/parentCandidateIds");
            for (int i = 0; i < count; i++)
            {
                string candidateId = plan.SessionId + "-c" + (i + 1).ToString("00");
                plan.Candidates.Add(new VfxGenerationCandidateSpec
                {
                    CandidateId = candidateId,
                    VariantKind = brief.Round <= 0 ? VfxRoundZeroVariantKinds.Ordered[i] : "ChildMutation" + (i + 1),
                    Seed = plan.FixedSeeds.Count > 0 ? plan.FixedSeeds[i % plan.FixedSeeds.Count] : 17,
                    ProviderId = provider.ProviderId,
                    ProviderVersion = provider.ProviderVersion,
                    ProviderGrantId = providerGrant != null ? providerGrant.GrantId : string.Empty,
                    ModelId = ResolveModelId(provider, providerGrant),
                    ModelVersion = ResolveModelVersion(provider, providerGrant),
                    TemplateId = template.TemplateId,
                    TemplateVersion = template.TemplateVersion,
                    CostTier = brief.TargetCostTier,
                    OutputRoot = plan.SandboxRoot + "/" + candidateId,
                    DraftOnly = true
                });
            }
            if (experimental)
                VfxReusePlanner.Add(plan.Diagnostics, CharacterAuthoringValidationSeverity.Info, VfxFlywheelDiagnosticCodes.ExperimentalDraftOnly, plan.PlanId, "Experimental arbitrary Graph topology candidates remain Draft-only and cannot be published directly.", "Rebuild through certified templates/subgraphs before promotion.", "generationMode");
            Finish(plan);
            return plan;
        }

        private static VfxGenerationProviderDefinition SelectProvider(VfxGenerationTemplateCatalogDocument templates, string preferred, VfxGenerationSessionCapabilities capabilities, out VfxGenerationProviderCapabilityGrant selectedGrant)
        {
            selectedGrant = null;
            if (!string.IsNullOrWhiteSpace(preferred))
                for (int i = 0; i < templates.Providers.Count; i++)
                    if (templates.Providers[i] != null && string.Equals(templates.Providers[i].ProviderId, preferred, StringComparison.Ordinal))
                    {
                        if (TryEnableProvider(templates.Providers[i], capabilities, out selectedGrant)) return templates.Providers[i];
                        return null;
                    }
            if (!string.IsNullOrWhiteSpace(preferred)) return null;
            string[] order = { VfxGenerationProviderKinds.BuiltInProcedural, VfxGenerationProviderKinds.LocalCli, VfxGenerationProviderKinds.LocalHttp, VfxGenerationProviderKinds.UnityTemplate };
            for (int kindIndex = 0; kindIndex < order.Length; kindIndex++)
                for (int i = 0; i < templates.Providers.Count; i++)
                    if (templates.Providers[i] != null && string.Equals(templates.Providers[i].Kind, order[kindIndex], StringComparison.Ordinal) && TryEnableProvider(templates.Providers[i], capabilities, out selectedGrant)) return templates.Providers[i];
            return null;
        }

        private static VfxGenerationTemplateDefinition SelectTemplate(VfxGenerationTemplateCatalogDocument templates, VfxCreativeBrief brief, VfxGenerationProviderDefinition provider, VfxGenerationSessionCapabilities capabilities)
        {
            if (provider == null) return null;
            VfxGenerationTemplateDefinition experimental = null;
            for (int i = 0; i < templates.Templates.Count; i++)
            {
                VfxGenerationTemplateDefinition template = templates.Templates[i];
                if (template == null || !string.Equals(template.Backend, brief.PreferredBackend, StringComparison.Ordinal) || !template.Roles.Exists(brief.Roles.Contains) ||
                    !template.CompatibleAnchors.Contains(brief.AnchorContract.Anchor) || template.AllowedProviderIds.Count > 0 && !template.AllowedProviderIds.Contains(provider.ProviderId)) continue;
                if (template.Certified && !template.ArbitraryTopology) return template;
                if (brief.AllowExperimentalGraphTopology && template.ArbitraryTopology && string.Equals(template.ExperimentalAdapterId, "unity-vfxgraph-17.5-experimental", StringComparison.Ordinal) && capabilities != null && capabilities.AllowedExperimentalAdapterIds.Contains(template.ExperimentalAdapterId)) experimental = template;
            }
            return experimental;
        }

        private static VfxQualityProfile FindQualityProfile(VfxGenerationTemplateCatalogDocument templates, string id, string version)
        {
            for (int i = 0; i < templates.QualityProfiles.Count; i++)
                if (templates.QualityProfiles[i] != null && string.Equals(templates.QualityProfiles[i].QualityProfileId, id, StringComparison.Ordinal) && string.Equals(templates.QualityProfiles[i].Version, version, StringComparison.Ordinal)) return templates.QualityProfiles[i];
            return null;
        }

        private static bool TryEnableProvider(VfxGenerationProviderDefinition provider, VfxGenerationSessionCapabilities capabilities, out VfxGenerationProviderCapabilityGrant grant)
        {
            grant = null;
            if (provider == null) return false;
            if (provider.EnabledByDefault)
                return !VfxGenerationTemplateCatalog.IsExternalModelProvider(provider.Kind) || VfxGenerationTemplateCatalog.HasExactModelBinding(provider);
            grant = FindProviderGrant(capabilities, provider.ProviderId);
            if (grant == null || !string.Equals(grant.ProviderVersion, provider.ProviderVersion, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(grant.ModelId) || string.IsNullOrWhiteSpace(grant.ModelVersion)) return false;
            if (VfxGenerationTemplateCatalog.HasExactModelBinding(provider) && (!string.Equals(grant.ModelId, provider.ModelId, StringComparison.Ordinal) || !string.Equals(grant.ModelVersion, provider.ModelVersion, StringComparison.Ordinal))) return false;
            if (string.Equals(provider.Kind, VfxGenerationProviderKinds.Cloud, StringComparison.Ordinal) && (capabilities == null || !capabilities.AllowCloud)) return false;
            return true;
        }

        private static VfxGenerationProviderCapabilityGrant FindProviderGrant(VfxGenerationSessionCapabilities capabilities, string providerId)
        {
            if (capabilities == null) return null;
            for (int i = 0; i < capabilities.ProviderGrants.Count; i++)
                if (capabilities.ProviderGrants[i] != null && string.Equals(capabilities.ProviderGrants[i].ProviderId, providerId, StringComparison.Ordinal)) return capabilities.ProviderGrants[i];
            return null;
        }

        private static string ResolveModelId(VfxGenerationProviderDefinition provider, VfxGenerationProviderCapabilityGrant grant) => grant != null ? grant.ModelId : provider.ModelId;
        private static string ResolveModelVersion(VfxGenerationProviderDefinition provider, VfxGenerationProviderCapabilityGrant grant) => grant != null ? grant.ModelVersion : provider.ModelVersion;

        private static void NormalizeCapabilities(VfxGenerationSessionCapabilities capabilities)
        {
            if (capabilities == null) return;
            capabilities.SchemaVersion = (capabilities.SchemaVersion ?? string.Empty).Trim();
            capabilities.CapabilityId = (capabilities.CapabilityId ?? string.Empty).Trim();
            capabilities.ProviderGrants ??= new List<VfxGenerationProviderCapabilityGrant>();
            capabilities.AllowedExperimentalAdapterIds ??= new List<string>();
            for (int i = capabilities.ProviderGrants.Count - 1; i >= 0; i--)
            {
                VfxGenerationProviderCapabilityGrant grant = capabilities.ProviderGrants[i];
                if (grant == null) { capabilities.ProviderGrants.RemoveAt(i); continue; }
                grant.GrantId = (grant.GrantId ?? string.Empty).Trim();
                grant.ProviderId = (grant.ProviderId ?? string.Empty).Trim();
                grant.ProviderVersion = (grant.ProviderVersion ?? string.Empty).Trim();
                grant.ModelId = (grant.ModelId ?? string.Empty).Trim();
                grant.ModelVersion = (grant.ModelVersion ?? string.Empty).Trim();
            }
            capabilities.ProviderGrants.Sort((left, right) => string.Compare(left != null ? left.ProviderId : string.Empty, right != null ? right.ProviderId : string.Empty, StringComparison.Ordinal));
            NormalizeCapabilityList(capabilities.AllowedExperimentalAdapterIds);
        }

        private static void NormalizeCapabilityList(List<string> values)
        {
            for (int i = 0; i < values.Count; i++) values[i] = (values[i] ?? string.Empty).Trim();
            values.Sort(StringComparer.Ordinal);
        }

        private static void ValidateCapabilities(VfxGenerationSessionCapabilities capabilities, VfxGenerationTemplateCatalogDocument templates, VfxGenerationPlan plan)
        {
            if (capabilities == null) return;
            if (!string.Equals(capabilities.SchemaVersion, VfxCreativeFlywheelSchemaVersions.SessionCapabilitiesV1, StringComparison.Ordinal) || !VfxReusePlanner.IsStableId(capabilities.CapabilityId))
                Add(plan, VfxFlywheelDiagnosticCodes.CapabilityInvalid, "Generation session capability schema/id is invalid.", "Use the versioned repository-external capability contract with a stable id.", "capabilities");
            var grantIds = new HashSet<string>(StringComparer.Ordinal);
            var providerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < capabilities.ProviderGrants.Count; i++)
            {
                VfxGenerationProviderCapabilityGrant grant = capabilities.ProviderGrants[i];
                VfxGenerationProviderDefinition provider = FindProvider(templates, grant != null ? grant.ProviderId : string.Empty);
                if (grant == null || !VfxReusePlanner.IsStableId(grant.GrantId) || !grantIds.Add(grant.GrantId) || !VfxReusePlanner.IsStableId(grant.ProviderId) || !providerIds.Add(grant.ProviderId) ||
                    string.IsNullOrWhiteSpace(grant.ProviderVersion) || string.IsNullOrWhiteSpace(grant.ModelId) || string.IsNullOrWhiteSpace(grant.ModelVersion) || provider == null || !string.Equals(provider.ProviderVersion, grant.ProviderVersion, StringComparison.Ordinal) ||
                    VfxGenerationTemplateCatalog.HasExactModelBinding(provider) && (!string.Equals(provider.ModelId, grant.ModelId, StringComparison.Ordinal) || !string.Equals(provider.ModelVersion, grant.ModelVersion, StringComparison.Ordinal)))
                    Add(plan, VfxFlywheelDiagnosticCodes.CapabilityInvalid, "Generation session contains an invalid, duplicate, unknown, or version-mismatched provider/model grant.", "Bind one stable grant to the exact catalog provider version and exact model id/version.", "capabilities/providerGrants/" + i);
            }
            ValidateCapabilityList(capabilities.AllowedExperimentalAdapterIds, "capabilities/allowedExperimentalAdapterIds", plan);
        }

        private static VfxGenerationProviderDefinition FindProvider(VfxGenerationTemplateCatalogDocument templates, string providerId)
        {
            for (int i = 0; i < templates.Providers.Count; i++)
                if (templates.Providers[i] != null && string.Equals(templates.Providers[i].ProviderId, providerId, StringComparison.Ordinal)) return templates.Providers[i];
            return null;
        }

        private static void ValidateCapabilityList(List<string> values, string field, VfxGenerationPlan plan)
        {
            for (int i = 0; i < values.Count; i++)
                if (!VfxReusePlanner.IsStableId(values[i]) || i > 0 && string.Equals(values[i], values[i - 1], StringComparison.Ordinal))
                    Add(plan, VfxFlywheelDiagnosticCodes.CapabilityInvalid, "Generation session capability contains an invalid or duplicate allowlist id: " + values[i], "Use unique stable ids.", field + "/" + i);
        }

        private static bool IsCostTier(string value) => string.Equals(value, VfxAuthoringCostTiers.Low, StringComparison.Ordinal) || string.Equals(value, VfxAuthoringCostTiers.Medium, StringComparison.Ordinal) || string.Equals(value, VfxAuthoringCostTiers.High, StringComparison.Ordinal);
        private static int TierRank(string value) => string.Equals(value, VfxAuthoringCostTiers.Low, StringComparison.Ordinal) ? 0 : string.Equals(value, VfxAuthoringCostTiers.Medium, StringComparison.Ordinal) ? 1 : 2;
        private static string NormalizeId(string value) => VfxReusePlanner.IsStableId(value) ? value.Trim().ToLowerInvariant() : "invalid";
        private static string NormalizeSessionId(string value) => NormalizeId(value).Replace('.', '-');
        private static string ShortHash(string hash) => hash != null && hash.Length >= 19 ? hash.Substring(7, 12) : "invalidhash";

        private static void Add(VfxGenerationPlan plan, string code, string message, string fix, string field) => VfxReusePlanner.Add(plan.Diagnostics, CharacterAuthoringValidationSeverity.Error, code, plan.PlanId, message, fix, field);
        private static void Finish(VfxGenerationPlan plan)
        {
            plan.Diagnostics.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));
            plan.PlanHash = VfxCreativeFlywheelHash.ComputeGenerationPlanHash(plan);
        }
    }
}
