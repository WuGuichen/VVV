using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MxFramework.Authoring
{
    /// <summary>Content hashes for human approval evidence that must remain immutable.</summary>
    public static class VfxAuthoringApprovalHasher
    {
        /// <summary>Computes the canonical Recipe reuse-qualification hash, excluding its self-hash field.</summary>
        /// <remarks>AllocByDesign. Authoring-only canonical JSON serialization and SHA-256.</remarks>
        public static string ComputeRecipeReuseQualificationHash(VfxAuthoringRecipeReuseQualification qualification)
        {
            return qualification == null ? string.Empty : VfxCreativeFlywheelHash.Compute(qualification, "qualificationHash");
        }
    }

    public static class VfxAuthoringDiagnosticCodes
    {
        public const string SchemaInvalid = "VFXAUTH001";
        public const string StableIdDuplicate = "VFXAUTH002";
        public const string ReferenceMissing = "VFXAUTH003";
        public const string RecipeInvalid = "VFXAUTH004";
        public const string ParameterInvalid = "VFXAUTH005";
        public const string CompatibilityInvalid = "VFXAUTH006";
        public const string BackendFallbackInvalid = "VFXAUTH007";
        public const string PreviewInvalid = "VFXAUTH008";
        public const string PublishedEffectStale = "VFXAUTH009";
        public const string CoverageMissing = "VFXAUTH010";
        public const string SimilarityCandidate = "VFXAUTH011";
        public const string ReadyGateFailed = "VFXAUTH012";
        public const string ApprovalEvidenceInvalid = "VFXAUTH013";
        public const string ReadyDependencyInvalid = "VFXAUTH014";
        public const string RecipeReuseQualificationInvalid = "VFXAUTH015";
        public const string VisualMaturityInvalid = "VFXAUTH016";
    }

    /// <summary>JSON loading and deterministic serialization for the VFX Authoring Catalog.</summary>
    public static class VfxAuthoringCatalogJson
    {
        /// <summary>Loads and normalizes a catalog from a JSON string without throwing recoverable parse failures.</summary>
        /// <remarks>LowFreqAlloc. Authoring-time JSON parsing and normalization.</remarks>
        public static VfxAuthoringCatalogLoadResult Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Failed("VFX Authoring Catalog JSON is empty.", string.Empty);

            try
            {
                VfxAuthoringCatalogDocument catalog = JsonSerializer.Deserialize<VfxAuthoringCatalogDocument>(json, CreateOptions());
                if (catalog == null)
                    return Failed("VFX Authoring Catalog JSON produced a null document.", string.Empty);

                VfxAuthoringCatalogNormalizer.NormalizeInPlace(catalog);
                return new VfxAuthoringCatalogLoadResult
                {
                    Success = true,
                    Catalog = catalog
                };
            }
            catch (JsonException ex)
            {
                return Failed("VFX Authoring Catalog JSON is invalid: " + ex.Message, ex.Path ?? string.Empty);
            }
        }

        /// <summary>Loads a catalog from disk and returns a recoverable diagnostic when the file cannot be read.</summary>
        /// <remarks>LowFreqAlloc. Authoring-time file IO and JSON parsing.</remarks>
        public static VfxAuthoringCatalogLoadResult LoadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return Failed("VFX Authoring Catalog file is missing: " + (path ?? string.Empty), path ?? string.Empty);

            try
            {
                return Load(File.ReadAllText(path));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Failed("VFX Authoring Catalog file could not be read: " + ex.Message, path);
            }
        }

        /// <summary>Normalizes and serializes a catalog using stable camelCase JSON.</summary>
        /// <remarks>AllocByDesign. Mutates ordering/whitespace fields in the supplied authoring DTO before allocating JSON.</remarks>
        public static string Serialize(VfxAuthoringCatalogDocument catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            VfxAuthoringCatalogNormalizer.NormalizeInPlace(catalog);
            return JsonSerializer.Serialize(catalog, CreateOptions());
        }

        private static JsonSerializerOptions CreateOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        private static VfxAuthoringCatalogLoadResult Failed(string message, string sourceField)
        {
            var result = new VfxAuthoringCatalogLoadResult();
            result.Diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(
                CharacterAuthoringValidationSeverity.Error,
                VfxAuthoringDiagnosticCodes.SchemaInvalid,
                string.Empty,
                string.Empty,
                message,
                "Repair the JSON document and retry.",
                sourceField));
            return result;
        }
    }

    /// <summary>Canonicalizes VFX Authoring Catalog identities and ordering before validation or save.</summary>
    public static class VfxAuthoringCatalogNormalizer
    {
        /// <summary>Normalizes null collections, trims stable text fields, and applies deterministic top-level/layer ordering.</summary>
        /// <remarks>LowFreqAlloc. Authoring-only; sorting and replacement collections may allocate.</remarks>
        public static void NormalizeInPlace(VfxAuthoringCatalogDocument catalog)
        {
            if (catalog == null)
                return;

            catalog.SchemaVersion = Trim(catalog.SchemaVersion);
            catalog.CatalogId = Trim(catalog.CatalogId);
            catalog.TaxonomyVersion = Trim(catalog.TaxonomyVersion);
            catalog.Primitives ??= new List<VfxAuthoringPrimitive>();
            catalog.Recipes ??= new List<VfxAuthoringRecipe>();
            catalog.Effects ??= new List<VfxAuthoringEffect>();

            for (int i = 0; i < catalog.Primitives.Count; i++)
                NormalizePrimitive(catalog.Primitives[i]);
            for (int i = 0; i < catalog.Recipes.Count; i++)
                NormalizeRecipe(catalog.Recipes[i]);
            for (int i = 0; i < catalog.Effects.Count; i++)
                NormalizeEffect(catalog.Effects[i]);

            catalog.Primitives.Sort((left, right) => Compare(left != null ? left.PrimitiveId : string.Empty, right != null ? right.PrimitiveId : string.Empty));
            catalog.Recipes.Sort((left, right) => Compare(left != null ? left.RecipeId : string.Empty, right != null ? right.RecipeId : string.Empty));
            catalog.Effects.Sort((left, right) => Compare(left != null ? left.EffectId : string.Empty, right != null ? right.EffectId : string.Empty));
        }

        private static void NormalizePrimitive(VfxAuthoringPrimitive primitive)
        {
            if (primitive == null)
                return;
            primitive.PrimitiveId = Trim(primitive.PrimitiveId);
            primitive.DisplayName = Trim(primitive.DisplayName);
            primitive.Description = Trim(primitive.Description);
            primitive.Backend = Trim(primitive.Backend);
            primitive.VisualMaturity = Default(primitive.VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder);
            primitive.ReviewStatus = Default(primitive.ReviewStatus, VfxAuthoringReviewStatuses.Draft);
            primitive.AssetRef ??= new VfxAuthoringAssetReference();
            primitive.AssetRef.Kind = Default(primitive.AssetRef.Kind, VfxAuthoringAssetReferenceKinds.UnityAsset);
            primitive.AssetRef.Path = NormalizePath(primitive.AssetRef.Path);
            if (primitive.Source != null)
            {
                primitive.Source.SourceKind = Trim(primitive.Source.SourceKind);
                primitive.Source.SourcePackageId = Trim(primitive.Source.SourcePackageId);
                primitive.Source.SourceLibraryId = Trim(primitive.Source.SourceLibraryId);
                primitive.Source.SourceEffectId = Trim(primitive.Source.SourceEffectId);
                primitive.Source.SourceComponentIds ??= new List<string>();
                primitive.Source.SourcePrefabGuid = Trim(primitive.Source.SourcePrefabGuid);
                primitive.Source.SourceObjectPath = Trim(primitive.Source.SourceObjectPath).Replace('\\', '/');
                primitive.Source.SourceContentHash = Trim(primitive.Source.SourceContentHash).ToLowerInvariant();
                primitive.Source.SourceDependencyHash = Trim(primitive.Source.SourceDependencyHash).ToLowerInvariant();
                primitive.Source.DerivationMode = Trim(primitive.Source.DerivationMode);
                primitive.Source.DependencyPolicy = Default(primitive.Source.DependencyPolicy, VfxSourceDependencyPolicies.DirectReference);
                primitive.Source.ExtractorId = Trim(primitive.Source.ExtractorId);
                primitive.Source.ExtractorVersion = Trim(primitive.Source.ExtractorVersion);
                primitive.Source.LicenseId = Trim(primitive.Source.LicenseId);
                NormalizeStringList(primitive.Source.SourceComponentIds, false);
            }
            primitive.Roles ??= new List<string>();
            primitive.CompatibleAnchors ??= new List<string>();
            primitive.Parameters ??= new List<VfxAuthoringParameterDefinition>();
            primitive.Tags ??= new List<string>();
            NormalizeApproval(primitive.Approval);
            NormalizeStringList(primitive.Roles, false);
            NormalizeStringList(primitive.CompatibleAnchors, true);
            NormalizeStringList(primitive.Tags, true);
            for (int i = 0; i < primitive.Parameters.Count; i++)
                NormalizeParameter(primitive.Parameters[i]);
            primitive.Parameters.Sort((left, right) => Compare(left != null ? left.ParameterId : string.Empty, right != null ? right.ParameterId : string.Empty));
        }

        private static void NormalizeRecipe(VfxAuthoringRecipe recipe)
        {
            if (recipe == null)
                return;
            recipe.RecipeId = Trim(recipe.RecipeId);
            recipe.DisplayName = Trim(recipe.DisplayName);
            recipe.Description = Trim(recipe.Description);
            recipe.VisualMaturity = Default(recipe.VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder);
            recipe.ReviewStatus = Default(recipe.ReviewStatus, VfxAuthoringReviewStatuses.Draft);
            recipe.Parameters ??= new List<string>();
            recipe.IncludedRecipeIds ??= new List<string>();
            recipe.Layers ??= new List<VfxAuthoringRecipeLayer>();
            recipe.Tags ??= new List<string>();
            NormalizeApproval(recipe.Approval);
            NormalizeRecipeReuseQualification(recipe.ReuseQualification);
            NormalizeStringList(recipe.Parameters, true);
            NormalizeStringList(recipe.IncludedRecipeIds, true);
            NormalizeStringList(recipe.Tags, true);
            for (int i = 0; i < recipe.Layers.Count; i++)
            {
                VfxAuthoringRecipeLayer layer = recipe.Layers[i];
                if (layer == null)
                    continue;
                layer.LayerId = Trim(layer.LayerId);
                layer.PrimitiveId = Trim(layer.PrimitiveId);
                layer.Bindings ??= new Dictionary<string, string>();
                layer.Overrides ??= new Dictionary<string, JsonElement>();
                layer.Bindings = NormalizeStringDictionary(layer.Bindings);
                layer.Overrides = NormalizeJsonDictionary(layer.Overrides);
            }
            recipe.Layers.Sort(CompareLayers);
        }

        private static void NormalizeRecipeReuseQualification(VfxAuthoringRecipeReuseQualification qualification)
        {
            if (qualification == null) return;
            qualification.SchemaVersion = Trim(qualification.SchemaVersion);
            qualification.QualificationId = Trim(qualification.QualificationId);
            qualification.SpecializedBaselineId = Trim(qualification.SpecializedBaselineId);
            qualification.QualificationHash = Trim(qualification.QualificationHash);
            qualification.Variants ??= new List<VfxAuthoringRecipeVariantQualification>();
            for (int i = qualification.Variants.Count - 1; i >= 0; i--)
            {
                VfxAuthoringRecipeVariantQualification variant = qualification.Variants[i];
                if (variant == null) { qualification.Variants.RemoveAt(i); continue; }
                variant.VariantId = Trim(variant.VariantId);
                variant.ThemeId = Trim(variant.ThemeId);
                variant.ScaleProfileId = Trim(variant.ScaleProfileId);
                variant.EvaluationReportHash = Trim(variant.EvaluationReportHash);
            }
            qualification.Variants.Sort((left, right) => Compare(left != null ? left.VariantId : string.Empty, right != null ? right.VariantId : string.Empty));
        }

        private static void NormalizeEffect(VfxAuthoringEffect effect)
        {
            if (effect == null)
                return;
            effect.EffectId = Trim(effect.EffectId);
            effect.DisplayName = Trim(effect.DisplayName);
            effect.Description = Trim(effect.Description);
            effect.RecipeId = Trim(effect.RecipeId);
            effect.Backend = Default(effect.Backend, VfxAuthoringBackends.ParticleSystem);
            effect.VisualMaturity = Default(effect.VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder);
            effect.ReviewStatus = Default(effect.ReviewStatus, VfxAuthoringReviewStatuses.Draft);
            effect.ResourceKey ??= new VfxAuthoringResourceKey();
            effect.ResourceKey.Id = Trim(effect.ResourceKey.Id);
            effect.ResourceKey.TypeId = Default(effect.ResourceKey.TypeId, "GameObject");
            effect.ResourceKey.Variant = Trim(effect.ResourceKey.Variant);
            effect.ResourceKey.PackageId = Trim(effect.ResourceKey.PackageId);
            effect.Roles ??= new List<string>();
            effect.ThemeIds ??= new List<string>();
            effect.ParameterOverrides ??= new Dictionary<string, JsonElement>();
            effect.ParameterOverrides = NormalizeJsonDictionary(effect.ParameterOverrides);
            effect.AnchorContract ??= new VfxAuthoringAnchorContract();
            effect.Lifecycle ??= new VfxAuthoringLifecycle();
            effect.Quality ??= new VfxAuthoringQuality();
            effect.Budget ??= new VfxAuthoringBudget();
            effect.Preview ??= new VfxAuthoringPreview();
            effect.Tags ??= new List<string>();
            NormalizeApproval(effect.Approval);
            effect.AnchorContract.Anchor = Default(effect.AnchorContract.Anchor, VfxAuthoringAnchors.World);
            effect.AnchorContract.Space = Default(effect.AnchorContract.Space, VfxAuthoringSpaces.World);
            effect.Lifecycle.Mode = Default(effect.Lifecycle.Mode, VfxAuthoringLifecycleModes.OneShot);
            effect.Lifecycle.StopPolicy = Default(effect.Lifecycle.StopPolicy, VfxAuthoringStopPolicies.Expire);
            effect.Quality.CostTier = Default(effect.Quality.CostTier, VfxAuthoringCostTiers.Medium);
            effect.Quality.FallbackPolicy = Default(effect.Quality.FallbackPolicy, VfxAuthoringFallbackPolicies.None);
            effect.Quality.FallbackEffectId = Trim(effect.Quality.FallbackEffectId);
            effect.Quality.PlatformWhitelist ??= new List<string>();
            effect.Budget.Priority = Default(effect.Budget.Priority, VfxAuthoringPriorities.Normal);
            effect.Preview.AnchorPreset = Trim(effect.Preview.AnchorPreset);
            NormalizeStringList(effect.Roles, false);
            NormalizeStringList(effect.ThemeIds, true);
            NormalizeStringList(effect.Quality.PlatformWhitelist, true);
            NormalizeStringList(effect.Tags, true);
        }

        private static void NormalizeParameter(VfxAuthoringParameterDefinition parameter)
        {
            if (parameter == null)
                return;
            parameter.ParameterId = Trim(parameter.ParameterId);
            parameter.ValueType = Default(parameter.ValueType, VfxAuthoringParameterValueTypes.Float);
            parameter.Unit = Trim(parameter.Unit);
            parameter.Transport = Default(parameter.Transport, VfxAuthoringParameterTransports.StaticBake);
            parameter.BackendProperty = Trim(parameter.BackendProperty);
        }

        private static void NormalizeApproval(VfxAuthoringApprovalEvidence approval)
        {
            if (approval == null)
                return;
            approval.ApprovalId = Trim(approval.ApprovalId);
            approval.ApprovedBy = Trim(approval.ApprovedBy);
            approval.ApprovalRole = Trim(approval.ApprovalRole);
            approval.AssetContentHash = Trim(approval.AssetContentHash);
            approval.DependencyHash = Trim(approval.DependencyHash);
            approval.RecipeInputHash = Trim(approval.RecipeInputHash);
            approval.EvaluationReportHash = Trim(approval.EvaluationReportHash);
            approval.QualityProfileVersion = Trim(approval.QualityProfileVersion);
            approval.RecipeReuseQualificationHash = Trim(approval.RecipeReuseQualificationHash);
            approval.ApprovalAuthorityId = Trim(approval.ApprovalAuthorityId);
            approval.ApprovalAuthorityVersion = Trim(approval.ApprovalAuthorityVersion);
            approval.AuthorityReceiptHash = Trim(approval.AuthorityReceiptHash);
            approval.HumanApprovals ??= new List<VfxAuthoringHumanApproval>();
            for (int i = approval.HumanApprovals.Count - 1; i >= 0; i--)
            {
                VfxAuthoringHumanApproval human = approval.HumanApprovals[i];
                if (human == null)
                {
                    approval.HumanApprovals.RemoveAt(i);
                    continue;
                }
                human.ReviewerId = Trim(human.ReviewerId);
                human.Role = Trim(human.Role);
            }
            approval.HumanApprovals.Sort((left, right) =>
            {
                int role = Compare(left != null ? left.Role : string.Empty, right != null ? right.Role : string.Empty);
                return role != 0 ? role : Compare(left != null ? left.ReviewerId : string.Empty, right != null ? right.ReviewerId : string.Empty);
            });
        }

        private static Dictionary<string, string> NormalizeStringDictionary(Dictionary<string, string> values)
        {
            var keys = new List<string>(values.Keys);
            keys.Sort(Compare);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
                result[keys[i]] = Trim(values[keys[i]]);
            return result;
        }

        private static Dictionary<string, JsonElement> NormalizeJsonDictionary(Dictionary<string, JsonElement> values)
        {
            var keys = new List<string>(values.Keys);
            keys.Sort(Compare);
            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
                result[keys[i]] = values[keys[i]];
            return result;
        }

        private static void NormalizeStringList(List<string> values, bool sort)
        {
            for (int i = values.Count - 1; i >= 0; i--)
            {
                values[i] = Trim(values[i]);
                if (values[i].Length == 0)
                    values.RemoveAt(i);
            }
            if (sort)
                values.Sort(Compare);
        }

        private static int CompareLayers(VfxAuthoringRecipeLayer left, VfxAuthoringRecipeLayer right)
        {
            int order = (left != null ? left.Order : 0).CompareTo(right != null ? right.Order : 0);
            if (order != 0)
                return order;
            return Compare(left != null ? left.LayerId : string.Empty, right != null ? right.LayerId : string.Empty);
        }

        private static int Compare(string left, string right) => string.Compare(left, right, StringComparison.Ordinal);
        private static string Trim(string value) => value == null ? string.Empty : value.Trim();
        private static string Default(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        private static string NormalizePath(string value) => Trim(value).Replace('\\', '/');
    }

    /// <summary>Validates VFX semantic identity, recipe composition, runtime bindings, review gates, and observed coverage.</summary>
    public static class VfxAuthoringCatalogValidator
    {
        /// <summary>Validates one catalog with optional scanner/resource/definition evidence.</summary>
        /// <remarks>LowFreqAlloc. Authoring-time validation builds maps, graph state, and diagnostics.</remarks>
        public static VfxAuthoringCatalogValidationReport Validate(
            VfxAuthoringCatalogDocument catalog,
            VfxAuthoringCatalogEvidence evidence = null)
        {
            var report = new VfxAuthoringCatalogValidationReport();
            if (catalog == null)
            {
                report.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, string.Empty, string.Empty, "VFX Authoring Catalog is missing.", "Create a catalog using mx.vfx.authoring.v1.", string.Empty));
                report.HasErrors = true;
                return report;
            }

            VfxAuthoringCatalogNormalizer.NormalizeInPlace(catalog);
            ValidateHeader(catalog, report.Diagnostics);

            var allIds = new HashSet<string>(StringComparer.Ordinal);
            var primitives = new Dictionary<string, VfxAuthoringPrimitive>(StringComparer.Ordinal);
            var recipes = new Dictionary<string, VfxAuthoringRecipe>(StringComparer.Ordinal);
            var effects = new Dictionary<string, VfxAuthoringEffect>(StringComparer.Ordinal);

            IndexPrimitives(catalog, allIds, primitives, report.Diagnostics);
            IndexRecipes(catalog, allIds, recipes, report.Diagnostics);
            IndexEffects(catalog, allIds, effects, report.Diagnostics);

            ValidatePrimitives(catalog, primitives, evidence, report.Diagnostics);
            ValidateRecipes(catalog, primitives, recipes, report.Diagnostics);
            ValidateRecipeCycles(catalog, recipes, report.Diagnostics);
            ValidateRecipeCompatibility(catalog, primitives, recipes, report.Diagnostics);
            ValidateEffects(catalog, primitives, recipes, effects, evidence, report.Diagnostics);
            ValidateCoverage(catalog, effects, evidence, report.Diagnostics);
            ValidateV2ReadyDependenciesAndApprovals(catalog, primitives, recipes, effects, evidence, report.Diagnostics);
            AddReadyGateDiagnostics(catalog, report.Diagnostics);

            report.Diagnostics.Sort(CompareDiagnostics);
            report.HasErrors = HasErrors(report.Diagnostics);
            return report;
        }

        internal static AuthoringResourceDiagnostic CreateDiagnostic(
            CharacterAuthoringValidationSeverity severity,
            string code,
            string stableId,
            string runtimeResourceKey,
            string message,
            string suggestedFix,
            string sourceField)
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ResourceId = stableId ?? string.Empty,
                ResourceStableId = stableId ?? string.Empty,
                RuntimeResourceKey = runtimeResourceKey ?? string.Empty,
                ProviderId = AuthoringResourceProviderIds.VfxAuthoring,
                SourceConfigKind = "vfxAuthoringCatalog",
                SourceStableId = stableId ?? string.Empty,
                SourceField = sourceField ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }

        private static void ValidateHeader(VfxAuthoringCatalogDocument catalog, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (!string.Equals(catalog.SchemaVersion, VfxAuthoringSchemaVersions.V1, StringComparison.Ordinal) &&
                !string.Equals(catalog.SchemaVersion, VfxAuthoringSchemaVersions.V2, StringComparison.Ordinal))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, catalog.CatalogId, string.Empty, "Unsupported VFX Authoring Catalog schemaVersion: " + catalog.SchemaVersion, "Use " + VfxAuthoringSchemaVersions.V2 + " (or read-only compatible " + VfxAuthoringSchemaVersions.V1 + ").", "schemaVersion");
            if (string.IsNullOrWhiteSpace(catalog.CatalogId) || !IsStableId(catalog.CatalogId))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, catalog.CatalogId, string.Empty, "catalogId is missing or contains unsupported characters.", "Use a stable lowercase id containing letters, digits, '.', '_', or '-'.", "catalogId");
            if (!string.Equals(catalog.TaxonomyVersion, VfxAuthoringTaxonomyVersions.V1, StringComparison.Ordinal))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, catalog.CatalogId, string.Empty, "Unsupported VFX taxonomyVersion: " + catalog.TaxonomyVersion, "Use " + VfxAuthoringTaxonomyVersions.V1 + ".", "taxonomyVersion");
        }

        private static void IndexPrimitives(VfxAuthoringCatalogDocument catalog, HashSet<string> allIds, Dictionary<string, VfxAuthoringPrimitive> result, List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < catalog.Primitives.Count; i++)
            {
                VfxAuthoringPrimitive item = catalog.Primitives[i];
                string id = item != null ? item.PrimitiveId : string.Empty;
                IndexId(id, "primitives/" + i + "/primitiveId", allIds, diagnostics);
                if (!string.IsNullOrWhiteSpace(id) && !result.ContainsKey(id))
                    result.Add(id, item);
            }
        }

        private static void IndexRecipes(VfxAuthoringCatalogDocument catalog, HashSet<string> allIds, Dictionary<string, VfxAuthoringRecipe> result, List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < catalog.Recipes.Count; i++)
            {
                VfxAuthoringRecipe item = catalog.Recipes[i];
                string id = item != null ? item.RecipeId : string.Empty;
                IndexId(id, "recipes/" + i + "/recipeId", allIds, diagnostics);
                if (!string.IsNullOrWhiteSpace(id) && !result.ContainsKey(id))
                    result.Add(id, item);
            }
        }

        private static void IndexEffects(VfxAuthoringCatalogDocument catalog, HashSet<string> allIds, Dictionary<string, VfxAuthoringEffect> result, List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect item = catalog.Effects[i];
                string id = item != null ? item.EffectId : string.Empty;
                IndexId(id, "effects/" + i + "/effectId", allIds, diagnostics);
                if (!string.IsNullOrWhiteSpace(id) && !result.ContainsKey(id))
                    result.Add(id, item);
            }
        }

        private static void IndexId(string id, string sourceField, HashSet<string> allIds, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(id) || !IsStableId(id))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, id, string.Empty, "VFX authoring stable id is missing or invalid.", "Use a stable lowercase id containing letters, digits, '.', '_', or '-'.", sourceField);
                return;
            }
            if (!allIds.Add(id))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.StableIdDuplicate, id, string.Empty, "Duplicate VFX authoring stable id: " + id, "Keep one Primitive, Recipe, or Effect with this id.", sourceField);
        }

        private static void ValidatePrimitives(VfxAuthoringCatalogDocument catalog, Dictionary<string, VfxAuthoringPrimitive> primitives, VfxAuthoringCatalogEvidence evidence, List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < catalog.Primitives.Count; i++)
            {
                VfxAuthoringPrimitive item = catalog.Primitives[i];
                if (item == null)
                    continue;
                string prefix = "primitives/" + i;
                ValidateSemanticFields(item.PrimitiveId, item.DisplayName, item.Description, item.ReviewStatus, prefix, diagnostics);
                ValidateVisualMaturity(item.PrimitiveId, item.VisualMaturity, item.ReviewStatus, item.Exemplar, prefix, diagnostics);
                if (!IsKnownBackend(item.Backend))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, item.PrimitiveId, string.Empty, "Unknown VFX backend: " + item.Backend, "Use ParticleSystem, LineRenderer, CustomView, or VfxGraph.", prefix + "/backend");
                ValidateRoles(item.PrimitiveId, item.Roles, prefix + "/roles", diagnostics);
                if (item.CompatibleAnchors.Count == 0)
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.CompatibilityInvalid, item.PrimitiveId, string.Empty, "Primitive must declare at least one compatible Anchor.", "Add a controlled compatibleAnchors value.", prefix + "/compatibleAnchors");
                for (int anchorIndex = 0; anchorIndex < item.CompatibleAnchors.Count; anchorIndex++)
                    if (!IsKnownAnchor(item.CompatibleAnchors[anchorIndex]))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.CompatibilityInvalid, item.PrimitiveId, string.Empty, "Unknown compatible Anchor: " + item.CompatibleAnchors[anchorIndex], "Use a controlled Anchor value.", prefix + "/compatibleAnchors/" + anchorIndex);

                string assetPath = item.AssetRef != null ? item.AssetRef.Path : string.Empty;
                if (item.AssetRef == null || !string.Equals(item.AssetRef.Kind, VfxAuthoringAssetReferenceKinds.UnityAsset, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(assetPath))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, item.PrimitiveId, string.Empty, "Primitive must reference one Unity authoring asset.", "Bind a project-relative Unity asset path.", prefix + "/assetRef");
                else if (evidence != null && evidence.UnityAssetPaths.Count > 0 && !evidence.UnityAssetPaths.Contains(assetPath))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, item.PrimitiveId, string.Empty, "Primitive Unity asset is missing: " + assetPath, "Restore the asset or update assetRef through the Unity authoring workflow.", prefix + "/assetRef/path");

                ValidateSourceEvidence(item, prefix + "/source", diagnostics);

                ValidateParameters(item.PrimitiveId, item.Parameters, prefix + "/parameters", diagnostics);
            }
        }

        private static void ValidateSourceEvidence(VfxAuthoringPrimitive primitive, string sourceField, List<AuthoringResourceDiagnostic> diagnostics)
        {
            VfxAuthoringSourceEvidence source = primitive.Source;
            if (source == null) return;
            bool valid = string.Equals(source.SourceKind, VfxAuthoringSourceKinds.LicensedThirdParty, StringComparison.Ordinal) &&
                         !string.IsNullOrWhiteSpace(source.SourcePackageId) && !string.IsNullOrWhiteSpace(source.SourceLibraryId) &&
                         !string.IsNullOrWhiteSpace(source.SourceEffectId) && source.SourceComponentIds != null && source.SourceComponentIds.Count > 0 &&
                         VfxCreativeFlywheelHash.IsUnityGuid(source.SourcePrefabGuid) &&
                         !string.IsNullOrWhiteSpace(source.SourceObjectPath) && VfxCreativeFlywheelHash.IsSha256(source.SourceContentHash) &&
                         VfxCreativeFlywheelHash.IsUnityDependencyHash(source.SourceDependencyHash) &&
                         (string.Equals(source.DerivationMode, VfxAuthoringDerivationModes.ExtractedLayer, StringComparison.Ordinal) || string.Equals(source.DerivationMode, VfxAuthoringDerivationModes.ParameterizedVariant, StringComparison.Ordinal)) &&
                         (string.Equals(source.DependencyPolicy, VfxSourceDependencyPolicies.DirectReference, StringComparison.Ordinal) || string.Equals(source.DependencyPolicy, VfxSourceDependencyPolicies.DerivedWhenModified, StringComparison.Ordinal)) &&
                         !string.IsNullOrWhiteSpace(source.ExtractorId) && !string.IsNullOrWhiteSpace(source.ExtractorVersion) &&
                         !string.IsNullOrWhiteSpace(source.LicenseId) && source.PublicRedistributionAllowed;
            if (!valid)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, primitive.PrimitiveId, string.Empty, "Licensed Primitive provenance is incomplete, stale, or not authorized for redistribution.", "Bind Source Library/effect/component ids, source Prefab GUID/object path, content/dependency hashes, extraction policy/version, license id, and redistribution authorization.", sourceField);
        }

        private static void ValidateRecipes(VfxAuthoringCatalogDocument catalog, Dictionary<string, VfxAuthoringPrimitive> primitives, Dictionary<string, VfxAuthoringRecipe> recipes, List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < catalog.Recipes.Count; i++)
            {
                VfxAuthoringRecipe recipe = catalog.Recipes[i];
                if (recipe == null)
                    continue;
                string prefix = "recipes/" + i;
                ValidateSemanticFields(recipe.RecipeId, recipe.DisplayName, recipe.Description, recipe.ReviewStatus, prefix, diagnostics);
                ValidateVisualMaturity(recipe.RecipeId, recipe.VisualMaturity, recipe.ReviewStatus, recipe.Exemplar, prefix, diagnostics);
                var recipeParameters = ToUniqueSet(recipe.Parameters, recipe.RecipeId, prefix + "/parameters", VfxAuthoringDiagnosticCodes.ParameterInvalid, diagnostics);
                var layerIds = new HashSet<string>(StringComparer.Ordinal);
                var layerOrders = new HashSet<int>();

                for (int dependencyIndex = 0; dependencyIndex < recipe.IncludedRecipeIds.Count; dependencyIndex++)
                {
                    string dependencyId = recipe.IncludedRecipeIds[dependencyIndex];
                    if (!recipes.ContainsKey(dependencyId))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, recipe.RecipeId, string.Empty, "Recipe includes a missing Recipe: " + dependencyId, "Restore the Recipe or remove the dependency.", prefix + "/includedRecipeIds/" + dependencyIndex);
                    else if (!string.Equals(recipe.VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder, StringComparison.Ordinal) &&
                             string.Equals(recipes[dependencyId].VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder, StringComparison.Ordinal))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.VisualMaturityInvalid, recipe.RecipeId, string.Empty, "A Candidate or Production Recipe cannot include a LegacyPlaceholder Recipe: " + dependencyId, "Rebuild the included Recipe from original Candidate/Production inputs.", prefix + "/includedRecipeIds/" + dependencyIndex);
                }

                for (int layerIndex = 0; layerIndex < recipe.Layers.Count; layerIndex++)
                {
                    VfxAuthoringRecipeLayer layer = recipe.Layers[layerIndex];
                    string layerPrefix = prefix + "/layers/" + layerIndex;
                    if (layer == null || string.IsNullOrWhiteSpace(layer.LayerId) || !layerIds.Add(layer.LayerId))
                    {
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.RecipeInvalid, recipe.RecipeId, string.Empty, "Recipe layer id is missing or duplicated.", "Use one stable layerId per Recipe layer.", layerPrefix + "/layerId");
                        continue;
                    }
                    if (!layerOrders.Add(layer.Order))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.RecipeInvalid, recipe.RecipeId, string.Empty, "Recipe layer order is duplicated: " + layer.Order, "Assign a unique deterministic order to every layer.", layerPrefix + "/order");
                    if (!primitives.TryGetValue(layer.PrimitiveId, out VfxAuthoringPrimitive primitive))
                    {
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, recipe.RecipeId, string.Empty, "Recipe layer references a missing Primitive: " + layer.PrimitiveId, "Restore the Primitive or update the layer reference.", layerPrefix + "/primitiveId");
                        continue;
                    }
                    if (!string.Equals(recipe.VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder, StringComparison.Ordinal) &&
                        string.Equals(primitive.VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder, StringComparison.Ordinal))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.VisualMaturityInvalid, recipe.RecipeId, string.Empty, "A Candidate or Production Recipe cannot consume a LegacyPlaceholder Primitive: " + primitive.PrimitiveId, "Create an original Candidate/Production Primitive before composing the Recipe.", layerPrefix + "/primitiveId");

                    var primitiveParameters = ParameterIds(primitive.Parameters);
                    foreach (KeyValuePair<string, string> binding in layer.Bindings)
                    {
                        if (!primitiveParameters.Contains(binding.Key) || !recipeParameters.Contains(binding.Value))
                            Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, recipe.RecipeId, string.Empty, "Recipe parameter binding is invalid: " + binding.Key + " -> " + binding.Value, "Bind an existing Primitive parameter to an existing Recipe parameter.", layerPrefix + "/bindings/" + binding.Key);
                    }
                    foreach (string overrideId in layer.Overrides.Keys)
                    {
                        VfxAuthoringParameterDefinition parameter = FindParameter(primitive.Parameters, overrideId);
                        if (parameter == null)
                            Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, recipe.RecipeId, string.Empty, "Recipe layer overrides an unknown Primitive parameter: " + overrideId, "Remove the override or declare the Primitive parameter.", layerPrefix + "/overrides/" + overrideId);
                        else
                            ValidateParameterValue(recipe.RecipeId, parameter, layer.Overrides[overrideId], layerPrefix + "/overrides/" + overrideId, diagnostics);
                    }
                }

                ValidateRecipeParameterContracts(recipe, primitives, diagnostics, prefix);
            }
        }

        private static void ValidateRecipeCompatibility(VfxAuthoringCatalogDocument catalog, Dictionary<string, VfxAuthoringPrimitive> primitives, Dictionary<string, VfxAuthoringRecipe> recipes, List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < catalog.Recipes.Count; i++)
            {
                VfxAuthoringRecipe recipe = catalog.Recipes[i];
                if (recipe == null)
                    continue;
                var used = new List<VfxAuthoringPrimitive>();
                CollectRecipePrimitives(recipe, primitives, recipes, new HashSet<string>(StringComparer.Ordinal), used);
                if (used.Count < 2)
                    continue;

                string backend = used[0].Backend;
                var compatibleAnchors = new HashSet<string>(used[0].CompatibleAnchors, StringComparer.Ordinal);
                for (int primitiveIndex = 1; primitiveIndex < used.Count; primitiveIndex++)
                {
                    VfxAuthoringPrimitive primitive = used[primitiveIndex];
                    if (!string.Equals(backend, primitive.Backend, StringComparison.Ordinal))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.CompatibilityInvalid, recipe.RecipeId, string.Empty, "Recipe combines incompatible Primitive backends: " + backend + " and " + primitive.Backend + ".", "Split the Recipe or use Primitives with one compatible backend.", "recipes/" + i + "/layers");
                    compatibleAnchors.IntersectWith(primitive.CompatibleAnchors);
                }
                if (compatibleAnchors.Count == 0)
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.CompatibilityInvalid, recipe.RecipeId, string.Empty, "Recipe Primitives have no compatible Anchor in common.", "Split the Recipe or update reviewed Primitive Anchor contracts.", "recipes/" + i + "/layers");
            }
        }

        private static void ValidateRecipeCycles(VfxAuthoringCatalogDocument catalog, Dictionary<string, VfxAuthoringRecipe> recipes, List<AuthoringResourceDiagnostic> diagnostics)
        {
            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Recipes.Count; i++)
            {
                VfxAuthoringRecipe recipe = catalog.Recipes[i];
                if (recipe != null && !string.IsNullOrWhiteSpace(recipe.RecipeId))
                    VisitRecipe(recipe, recipes, states, diagnostics);
            }
        }

        private static void VisitRecipe(VfxAuthoringRecipe recipe, Dictionary<string, VfxAuthoringRecipe> recipes, Dictionary<string, int> states, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (states.TryGetValue(recipe.RecipeId, out int state))
            {
                if (state == 1)
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.RecipeInvalid, recipe.RecipeId, string.Empty, "Recipe dependency cycle detected at " + recipe.RecipeId, "Remove recursive Recipe inclusion.", "includedRecipeIds");
                return;
            }

            states[recipe.RecipeId] = 1;
            for (int i = 0; i < recipe.IncludedRecipeIds.Count; i++)
                if (recipes.TryGetValue(recipe.IncludedRecipeIds[i], out VfxAuthoringRecipe dependency))
                    VisitRecipe(dependency, recipes, states, diagnostics);
            states[recipe.RecipeId] = 2;
        }

        private static void ValidateEffects(VfxAuthoringCatalogDocument catalog, Dictionary<string, VfxAuthoringPrimitive> primitives, Dictionary<string, VfxAuthoringRecipe> recipes, Dictionary<string, VfxAuthoringEffect> effects, VfxAuthoringCatalogEvidence evidence, List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect effect = catalog.Effects[i];
                if (effect == null)
                    continue;
                string prefix = "effects/" + i;
                string resourceKey = effect.ResourceKey != null ? effect.ResourceKey.ToStableKey() : string.Empty;
                ValidateSemanticFields(effect.EffectId, effect.DisplayName, effect.Description, effect.ReviewStatus, prefix, diagnostics);
                ValidateVisualMaturity(effect.EffectId, effect.VisualMaturity, effect.ReviewStatus, effect.Exemplar, prefix, diagnostics);
                if (!IsKnownBackend(effect.Backend))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, effect.EffectId, resourceKey, "Unknown VFX backend: " + effect.Backend, "Use a controlled backend value.", prefix + "/backend");
                ValidateRoles(effect.EffectId, effect.Roles, prefix + "/roles", diagnostics);
                ValidateResourceKey(effect, resourceKey, evidence, prefix, diagnostics);
                ValidateEffectRecipe(effect, primitives, recipes, evidence, prefix, diagnostics);
                ValidateEffectCompatibility(effect, prefix, diagnostics);
                ValidateEffectQuality(effect, effects, evidence, prefix, diagnostics);
                ValidateEffectPreviewAndBudget(effect, prefix, diagnostics);
                if (evidence != null && evidence.StaleEffectIds.Contains(effect.EffectId))
                    Add(diagnostics, ReadySensitiveSeverity(effect), VfxAuthoringDiagnosticCodes.PublishedEffectStale, effect.EffectId, resourceKey, "Published Effect is stale relative to its Recipe or Primitive inputs.", "Re-bake and revalidate the Published Effect.", prefix);
            }
        }

        private static void ValidateResourceKey(VfxAuthoringEffect effect, string stableKey, VfxAuthoringCatalogEvidence evidence, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            VfxAuthoringResourceKey key = effect.ResourceKey;
            if (key == null || !IsStableId(key.Id) || !string.Equals(key.TypeId, "GameObject", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(key.PackageId))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, effect.EffectId, stableKey, "Effect must bind a valid package-scoped GameObject ResourceKey.", "Set resourceKey.id, typeId=GameObject, variant, and packageId.", prefix + "/resourceKey");
            else if (evidence != null && evidence.ResourceKeys.Count > 0 && !evidence.ResourceKeys.Contains(stableKey))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, effect.EffectId, stableKey, "Effect ResourceKey is missing from observed Resource Catalog evidence.", "Restore the Resource Catalog entry or update the Effect binding.", prefix + "/resourceKey");

            if (effect.DefinitionId < 0)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, effect.EffectId, stableKey, "definitionId cannot be negative.", "Use 0 for catalog-only effects or a positive VfxDefinitionId.", prefix + "/definitionId");
            else if (effect.DefinitionId > 0 && evidence != null && evidence.DefinitionIds.Count > 0 && !evidence.DefinitionIds.Contains(effect.DefinitionId))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, effect.EffectId, stableKey, "Effect definitionId is missing from observed VFX definitions: " + effect.DefinitionId, "Restore the definition or update definitionId.", prefix + "/definitionId");
        }

        private static void ValidateEffectRecipe(VfxAuthoringEffect effect, Dictionary<string, VfxAuthoringPrimitive> primitives, Dictionary<string, VfxAuthoringRecipe> recipes, VfxAuthoringCatalogEvidence evidence, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(effect.RecipeId))
            {
                if (effect.ParameterOverrides.Count > 0)
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect parameterOverrides require a Recipe parameter contract.", "Bind a Recipe or remove Recipe parameter overrides.", prefix + "/parameterOverrides");
                return;
            }

            if (!recipes.TryGetValue(effect.RecipeId, out VfxAuthoringRecipe recipe))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect references a missing Recipe: " + effect.RecipeId, "Restore the Recipe or clear recipeId for a manually authored Effect.", prefix + "/recipeId");
                return;
            }
            if (!string.Equals(effect.VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder, StringComparison.Ordinal) &&
                string.Equals(recipe.VisualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder, StringComparison.Ordinal))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.VisualMaturityInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "A Candidate or Production Effect cannot publish a LegacyPlaceholder Recipe: " + recipe.RecipeId, "Rebuild the Recipe from original Candidate/Production Primitives before publishing.", prefix + "/recipeId");

            if (effect.Published == null || string.IsNullOrWhiteSpace(effect.Published.AssetPath) ||
                string.IsNullOrWhiteSpace(effect.Published.BakerId) || string.IsNullOrWhiteSpace(effect.Published.BakerVersion))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReferenceMissing, effect.EffectId, effect.ResourceKey.ToStableKey(), "Recipe-backed Effect requires published asset path and baker identity.", "Set published.assetPath, bakerId, and bakerVersion before baking.", prefix + "/published");
            }
            else if (evidence != null && evidence.UnityAssetPaths.Count > 0 && !evidence.UnityAssetPaths.Contains(effect.Published.AssetPath))
            {
                Add(diagnostics, ReadySensitiveSeverity(effect), VfxAuthoringDiagnosticCodes.PublishedEffectStale, effect.EffectId, effect.ResourceKey.ToStableKey(), "Published Effect prefab is missing: " + effect.Published.AssetPath, "Run the formal Unity Recipe baker.", prefix + "/published/assetPath");
            }

            Dictionary<string, VfxAuthoringParameterDefinition> recipeParameters = BuildRecipeParameterContracts(recipe, primitives);
            foreach (string parameterId in effect.ParameterOverrides.Keys)
            {
                if (!recipeParameters.TryGetValue(parameterId, out VfxAuthoringParameterDefinition parameter))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect overrides an unknown Recipe parameter: " + parameterId, "Remove the override or declare the Recipe parameter.", prefix + "/parameterOverrides/" + parameterId);
                else
                    ValidateParameterValue(effect.EffectId, parameter, effect.ParameterOverrides[parameterId], prefix + "/parameterOverrides/" + parameterId, diagnostics);
            }

            var used = new List<VfxAuthoringPrimitive>();
            CollectRecipePrimitives(recipe, primitives, recipes, new HashSet<string>(StringComparer.Ordinal), used);
            for (int i = 0; i < used.Count; i++)
            {
                VfxAuthoringPrimitive primitive = used[i];
                if (!string.Equals(effect.Backend, primitive.Backend, StringComparison.Ordinal))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.CompatibilityInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect backend " + effect.Backend + " is incompatible with Recipe Primitive " + primitive.PrimitiveId + " backend " + primitive.Backend + ".", "Use a compatible Recipe or correct the reviewed backend contract.", prefix + "/backend");
                if (effect.AnchorContract != null && !primitive.CompatibleAnchors.Contains(effect.AnchorContract.Anchor))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.CompatibilityInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect Anchor " + effect.AnchorContract.Anchor + " is incompatible with Recipe Primitive " + primitive.PrimitiveId + ".", "Use a compatible Recipe or update the reviewed Primitive Anchor contract.", prefix + "/anchorContract/anchor");
            }
        }

        private static void ValidateEffectCompatibility(VfxAuthoringEffect effect, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (effect.AnchorContract == null || !IsKnownAnchor(effect.AnchorContract.Anchor) || !IsKnownSpace(effect.AnchorContract.Space))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.CompatibilityInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect Anchor or space is invalid.", "Use a controlled Anchor and Local/World space.", prefix + "/anchorContract");
            if (effect.Lifecycle == null || !IsKnownLifecycle(effect.Lifecycle.Mode) || !IsKnownStopPolicy(effect.Lifecycle.StopPolicy) || effect.Lifecycle.DefaultLifetimeFrames < 0)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.CompatibilityInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect lifecycle is invalid.", "Use a controlled lifecycle/stop policy and a non-negative lifetime.", prefix + "/lifecycle");
            else if (string.Equals(effect.Lifecycle.Mode, VfxAuthoringLifecycleModes.OneShot, StringComparison.Ordinal) && effect.Lifecycle.DefaultLifetimeFrames <= 0)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.CompatibilityInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "OneShot Effect requires a positive defaultLifetimeFrames.", "Set the authored one-shot lifetime.", prefix + "/lifecycle/defaultLifetimeFrames");
        }

        private static void ValidateEffectQuality(VfxAuthoringEffect effect, Dictionary<string, VfxAuthoringEffect> effects, VfxAuthoringCatalogEvidence evidence, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            VfxAuthoringQuality quality = effect.Quality;
            if (quality == null || !IsKnownCostTier(quality.CostTier) || !IsKnownFallbackPolicy(quality.FallbackPolicy))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect quality contract is invalid.", "Use a controlled costTier and fallbackPolicy.", prefix + "/quality");
                return;
            }
            if (string.Equals(quality.FallbackPolicy, VfxAuthoringFallbackPolicies.UseFallbackEffect, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(quality.FallbackEffectId) || !effects.ContainsKey(quality.FallbackEffectId)))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.BackendFallbackInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Fallback Effect is missing: " + quality.FallbackEffectId, "Bind an existing fallbackEffectId.", prefix + "/quality/fallbackEffectId");

            if (string.Equals(effect.Backend, VfxAuthoringBackends.VfxGraph, StringComparison.Ordinal))
            {
                bool graphAvailable = evidence == null || evidence.VfxGraphAvailable;
                bool hasFallback = !string.Equals(quality.FallbackPolicy, VfxAuthoringFallbackPolicies.None, StringComparison.Ordinal);
                bool hasWhitelist = quality.PlatformWhitelist.Count > 0;
                CharacterAuthoringValidationSeverity severity = string.Equals(effect.ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal)
                    ? CharacterAuthoringValidationSeverity.Error
                    : CharacterAuthoringValidationSeverity.Warning;
                if (!graphAvailable || (!hasFallback && !hasWhitelist))
                    Add(diagnostics, severity, VfxAuthoringDiagnosticCodes.BackendFallbackInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "VFX Graph Effect requires available Graph support plus a fallback or explicit platform whitelist.", "Add a Resource Variant/fallback Effect or declare the supported platform whitelist after validation.", prefix + "/quality");
            }
        }

        private static void ValidateEffectPreviewAndBudget(VfxAuthoringEffect effect, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (effect.Preview == null || string.IsNullOrWhiteSpace(effect.Preview.AnchorPreset) || effect.Preview.DurationSeconds <= 0f || effect.Preview.CameraScaleMeters <= 0f)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Warning, VfxAuthoringDiagnosticCodes.PreviewInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect preview preset is missing or incomplete.", "Set anchorPreset, durationSeconds, and cameraScaleMeters for repeatable Showcase review.", prefix + "/preview");
            if (effect.Budget == null || effect.Budget.PrewarmCount < 0 || effect.Budget.RecommendedMaxConcurrent < 0 || effect.Budget.CullDistanceMeters < 0f || !IsKnownPriority(effect.Budget.Priority))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Effect budget contract is invalid.", "Use non-negative counts/distances and a controlled priority.", prefix + "/budget");
        }

        private static void ValidateCoverage(VfxAuthoringCatalogDocument catalog, Dictionary<string, VfxAuthoringEffect> effects, VfxAuthoringCatalogEvidence evidence, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (evidence == null || evidence.DirectVfxResourceKeys.Count == 0)
                return;
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (VfxAuthoringEffect effect in effects.Values)
                if (effect != null && effect.ResourceKey != null)
                    covered.Add(effect.ResourceKey.ToStableKey());
            foreach (string key in evidence.DirectVfxResourceKeys)
            {
                if (covered.Contains(key))
                    continue;
                Add(diagnostics,
                    catalog.StrictCoverage ? CharacterAuthoringValidationSeverity.Error : CharacterAuthoringValidationSeverity.Warning,
                    VfxAuthoringDiagnosticCodes.CoverageMissing,
                    catalog.CatalogId,
                    key,
                    "Direct domain.vfx ResourceKey has no Effect semantic entry: " + key,
                    "Add a Draft/ReviewNeeded Effect entry or explicitly remove the resource from direct VFX coverage.",
                    "effects");
            }
        }

        private static void ValidateV2ReadyDependenciesAndApprovals(
            VfxAuthoringCatalogDocument catalog,
            Dictionary<string, VfxAuthoringPrimitive> primitives,
            Dictionary<string, VfxAuthoringRecipe> recipes,
            Dictionary<string, VfxAuthoringEffect> effects,
            VfxAuthoringCatalogEvidence evidence,
            List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (!string.Equals(catalog.SchemaVersion, VfxAuthoringSchemaVersions.V2, StringComparison.Ordinal))
                return;

            for (int i = 0; i < catalog.Primitives.Count; i++)
            {
                VfxAuthoringPrimitive primitive = catalog.Primitives[i];
                if (primitive == null)
                    continue;
                ValidateExemplarStatus(primitive.PrimitiveId, primitive.Exemplar, primitive.ReviewStatus, "primitives/" + i, diagnostics);
                if (string.Equals(primitive.ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                    ValidateApprovalEvidence(primitive.PrimitiveId, primitive.Approval, false, false, evidence, "primitives/" + i + "/approval", diagnostics);
            }

            for (int i = 0; i < catalog.Recipes.Count; i++)
            {
                VfxAuthoringRecipe recipe = catalog.Recipes[i];
                if (recipe == null)
                    continue;
                string prefix = "recipes/" + i;
                ValidateExemplarStatus(recipe.RecipeId, recipe.Exemplar, recipe.ReviewStatus, prefix, diagnostics);
                if (!string.Equals(recipe.ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                    continue;
                ValidateApprovalEvidence(recipe.RecipeId, recipe.Approval, false, false, evidence, prefix + "/approval", diagnostics);
                if (recipe.Canonical)
                    ValidateRecipeReuseQualification(recipe, prefix, diagnostics);
                var dependencyIds = new HashSet<string>(StringComparer.Ordinal);
                CollectRecipeDependencyIds(recipe, recipes, dependencyIds);
                foreach (string dependencyId in dependencyIds)
                {
                    if (recipes.TryGetValue(dependencyId, out VfxAuthoringRecipe dependency) &&
                        !string.Equals(dependency.ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReadyDependencyInvalid, recipe.RecipeId, string.Empty, "Ready Recipe depends on a non-Ready Recipe: " + dependencyId, "Complete human review for every transitive Recipe dependency.", prefix + "/includedRecipeIds");
                }
                var usedPrimitives = new List<VfxAuthoringPrimitive>();
                CollectRecipePrimitives(recipe, primitives, recipes, new HashSet<string>(StringComparer.Ordinal), usedPrimitives);
                for (int primitiveIndex = 0; primitiveIndex < usedPrimitives.Count; primitiveIndex++)
                    if (!string.Equals(usedPrimitives[primitiveIndex].ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReadyDependencyInvalid, recipe.RecipeId, string.Empty, "Ready Recipe depends on a non-Ready Primitive: " + usedPrimitives[primitiveIndex].PrimitiveId, "Complete human review for every transitive Primitive dependency.", prefix + "/layers");
            }

            for (int i = 0; i < catalog.Effects.Count; i++)
            {
                VfxAuthoringEffect effect = catalog.Effects[i];
                if (effect == null)
                    continue;
                string prefix = "effects/" + i;
                ValidateExemplarStatus(effect.EffectId, effect.Exemplar, effect.ReviewStatus, prefix, diagnostics);
                if (!string.Equals(effect.ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                    continue;

                bool requiresGameplayApproval = effect.Budget != null &&
                    (string.Equals(effect.Budget.Priority, VfxAuthoringPriorities.GameplayImportant, StringComparison.Ordinal) ||
                     string.Equals(effect.Budget.Priority, VfxAuthoringPriorities.CriticalTelegraph, StringComparison.Ordinal));
                ValidateApprovalEvidence(effect.EffectId, effect.Approval, !string.IsNullOrWhiteSpace(effect.RecipeId), requiresGameplayApproval, evidence, prefix + "/approval", diagnostics);
                if (!string.IsNullOrWhiteSpace(effect.RecipeId) && effect.Approval != null && effect.Published != null &&
                    !string.Equals(effect.Approval.RecipeInputHash, effect.Published.RecipeInputHash, StringComparison.OrdinalIgnoreCase))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Ready approval Recipe input hash is stale relative to Published Effect evidence.", "Re-bake, re-evaluate, and repeat human approval.", prefix + "/approval/recipeInputHash");

                if (!string.IsNullOrWhiteSpace(effect.RecipeId) && recipes.TryGetValue(effect.RecipeId, out VfxAuthoringRecipe recipe))
                {
                    if (!string.Equals(recipe.ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReadyDependencyInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Ready Effect depends on a non-Ready Recipe: " + recipe.RecipeId, "Complete Recipe and transitive dependency review before approving the Effect.", prefix + "/recipeId");
                    var usedPrimitives = new List<VfxAuthoringPrimitive>();
                    CollectRecipePrimitives(recipe, primitives, recipes, new HashSet<string>(StringComparer.Ordinal), usedPrimitives);
                    for (int primitiveIndex = 0; primitiveIndex < usedPrimitives.Count; primitiveIndex++)
                        if (!string.Equals(usedPrimitives[primitiveIndex].ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                            Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReadyDependencyInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Ready Effect depends on a non-Ready Primitive: " + usedPrimitives[primitiveIndex].PrimitiveId, "Complete Primitive review before approving the Effect.", prefix + "/recipeId");
                }

                if (effect.Quality != null && string.Equals(effect.Quality.FallbackPolicy, VfxAuthoringFallbackPolicies.UseFallbackEffect, StringComparison.Ordinal) &&
                    effects.TryGetValue(effect.Quality.FallbackEffectId, out VfxAuthoringEffect fallback))
                {
                    if (!string.Equals(fallback.ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReadyDependencyInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Ready Effect fallback is not Ready: " + fallback.EffectId, "Complete fallback review before approving the primary Effect.", prefix + "/quality/fallbackEffectId");
                    if (!AreFallbackContractsCompatible(effect, fallback))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReadyDependencyInvalid, effect.EffectId, effect.ResourceKey.ToStableKey(), "Ready Effect fallback has an incompatible Anchor or lifecycle contract: " + fallback.EffectId, "Use a fallback with the same Anchor, space, lifecycle mode, and stop policy.", prefix + "/quality/fallbackEffectId");
                }
            }
        }

        private static void ValidateRecipeReuseQualification(VfxAuthoringRecipe recipe, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            VfxAuthoringRecipeReuseQualification qualification = recipe.ReuseQualification;
            if (qualification == null || !string.Equals(qualification.SchemaVersion, "mx.vfx.recipe-reuse-qualification.v1", StringComparison.Ordinal) ||
                !IsStableId(qualification.QualificationId) || !IsStableId(qualification.SpecializedBaselineId))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.RecipeReuseQualificationInvalid, recipe.RecipeId, string.Empty, "Canonical Ready Recipe requires versioned reuse-qualification evidence and a specialized baseline id.", "Evaluate at least two theme/scale variants against a specialized implementation before Ready approval.", prefix + "/reuseQualification");
                return;
            }
            string observedHash = VfxAuthoringApprovalHasher.ComputeRecipeReuseQualificationHash(qualification);
            if (!IsSha256(qualification.QualificationHash) || !string.Equals(qualification.QualificationHash, observedHash, StringComparison.OrdinalIgnoreCase) || recipe.Approval == null || !string.Equals(recipe.Approval.RecipeReuseQualificationHash, observedHash, StringComparison.OrdinalIgnoreCase))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.RecipeReuseQualificationInvalid, recipe.RecipeId, string.Empty, "Canonical Recipe qualification hash is missing, tampered, or not bound by the human approval.", "Regenerate immutable qualification evidence and repeat Ready approval.", prefix + "/reuseQualification/qualificationHash");
            var variantIds = new HashSet<string>(StringComparer.Ordinal);
            var distinctProfiles = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < qualification.Variants.Count; i++)
            {
                VfxAuthoringRecipeVariantQualification variant = qualification.Variants[i];
                if (variant == null || !IsStableId(variant.VariantId) || !variantIds.Add(variant.VariantId) ||
                    string.IsNullOrWhiteSpace(variant.ThemeId) && string.IsNullOrWhiteSpace(variant.ScaleProfileId) ||
                    variant.QualityDegradationPercent < 0f || variant.QualityDegradationPercent > 5f ||
                    variant.PerformanceDegradationPercent < 0f || variant.PerformanceDegradationPercent > 5f || !IsSha256(variant.EvaluationReportHash))
                {
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.RecipeReuseQualificationInvalid, recipe.RecipeId, string.Empty, "Canonical Recipe variant evidence is invalid or exceeds the 5% quality/performance degradation limit.", "Provide unique variant ids, theme/scale identity, immutable evaluation hash, and both degradations within 0..5%.", prefix + "/reuseQualification/variants/" + i);
                    continue;
                }
                distinctProfiles.Add(variant.ThemeId + "|" + variant.ScaleProfileId);
            }
            if (qualification.Variants.Count < 2 || distinctProfiles.Count < 2)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.RecipeReuseQualificationInvalid, recipe.RecipeId, string.Empty, "Canonical Recipe requires two distinct theme or scale variants.", "Evaluate two genuinely distinct variant profiles before Ready approval.", prefix + "/reuseQualification/variants");
        }

        private static void ValidateExemplarStatus(string stableId, bool exemplar, string reviewStatus, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (exemplar && !string.Equals(reviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, "Only a human-approved Ready entry may be marked as an exemplar.", "Remove exemplar=true or complete the Ready approval workflow.", prefix + "/exemplar");
        }

        private static void ValidateApprovalEvidence(string stableId, VfxAuthoringApprovalEvidence approval, bool requireRecipeHash, bool requireGameplayApproval, VfxAuthoringCatalogEvidence evidence, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (approval == null || string.IsNullOrWhiteSpace(approval.ApprovalId) ||
                !IsSha256(approval.AssetContentHash) || !IsSha256(approval.DependencyHash) ||
                !IsSha256(approval.EvaluationReportHash) || string.IsNullOrWhiteSpace(approval.QualityProfileVersion) ||
                (requireRecipeHash && !IsSha256(approval.RecipeInputHash)) ||
                string.IsNullOrWhiteSpace(approval.ApprovalAuthorityId) || string.IsNullOrWhiteSpace(approval.ApprovalAuthorityVersion) ||
                !IsSha256(approval.AuthorityReceiptHash))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, "Ready v2 entry requires complete immutable approval and authority-receipt evidence.", "Bind approval id, asset/dependency/evaluation hashes, quality profile version, Recipe input hash when applicable, and a versioned approval-authority receipt hash.", prefix);
                return;
            }

            ValidateApprovalAuthority(stableId, approval, evidence != null ? evidence.ApprovalAuthorityVerifier : null, prefix, diagnostics);

            bool hasArtApproval = IsArtApprovalRole(approval.ApprovalRole) && !string.IsNullOrWhiteSpace(approval.ApprovedBy);
            bool hasGameplayApproval = IsGameplayApprovalRole(approval.ApprovalRole) && !string.IsNullOrWhiteSpace(approval.ApprovedBy);
            for (int i = 0; i < approval.HumanApprovals.Count; i++)
            {
                VfxAuthoringHumanApproval human = approval.HumanApprovals[i];
                if (human == null || string.IsNullOrWhiteSpace(human.ReviewerId))
                    continue;
                hasArtApproval |= IsArtApprovalRole(human.Role);
                hasGameplayApproval |= IsGameplayApprovalRole(human.Role);
            }
            if (!hasArtApproval || (requireGameplayApproval && !hasGameplayApproval))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, requireGameplayApproval ? "Ready gameplay-relevant Effect requires VFX/technical-art and gameplay/UX approvals." : "Ready entry requires an authorized VFX or technical-art approval.", "Add explicit human approval records; automated evaluation cannot approve Ready.", prefix + "/humanApprovals");

            ValidateObservedApprovalHash(stableId, approval.AssetContentHash, evidence != null ? evidence.AssetContentHashes : null, "asset content", prefix + "/assetContentHash", diagnostics);
            ValidateObservedApprovalHash(stableId, approval.DependencyHash, evidence != null ? evidence.DependencyHashes : null, "dependency", prefix + "/dependencyHash", diagnostics);
            ValidateObservedApprovalHash(stableId, approval.EvaluationReportHash, evidence != null ? evidence.EvaluationReportHashes : null, "evaluation report", prefix + "/evaluationReportHash", diagnostics);
            ValidateObservedQualityProfileVersion(stableId, approval.QualityProfileVersion, evidence != null ? evidence.QualityProfileVersions : null, prefix + "/qualityProfileVersion", diagnostics);
        }

        private static void ValidateApprovalAuthority(string stableId, VfxAuthoringApprovalEvidence approval, IVfxReadyApprovalAuthorityVerifier verifier, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (verifier == null)
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, "Ready approval is fail-closed because no trusted approval-authority verifier is configured.", "Configure an authenticated authority verifier in the host process; Catalog reviewer strings and hashes cannot authorize Ready.", prefix + "/authorityReceiptHash");
                return;
            }
            if (!string.Equals(verifier.AuthorityId, approval.ApprovalAuthorityId, StringComparison.Ordinal) ||
                !string.Equals(verifier.AuthorityVersion, approval.ApprovalAuthorityVersion, StringComparison.Ordinal))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, "Ready approval authority id/version is not trusted by the configured verifier.", "Use a receipt issued by the configured approval authority and repeat approval.", prefix + "/approvalAuthorityId");
                return;
            }

            var request = new VfxReadyApprovalAuthorityRequest
            {
                StableId = stableId,
                ApprovalId = approval.ApprovalId,
                ApprovedBy = approval.ApprovedBy,
                ApprovalRole = approval.ApprovalRole,
                AuthorityReceiptHash = approval.AuthorityReceiptHash,
                AssetContentHash = approval.AssetContentHash,
                DependencyHash = approval.DependencyHash,
                RecipeInputHash = approval.RecipeInputHash,
                EvaluationReportHash = approval.EvaluationReportHash,
                QualityProfileVersion = approval.QualityProfileVersion,
                RecipeReuseQualificationHash = approval.RecipeReuseQualificationHash,
                HumanApprovals = approval.HumanApprovals
            };
            bool verified;
            string rejectionReason;
            try
            {
                verified = verifier.Verify(request, out rejectionReason);
            }
            catch (Exception ex)
            {
                verified = false;
                rejectionReason = ex.Message;
            }
            if (!verified)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, "Ready approval authority receipt was rejected" + (string.IsNullOrWhiteSpace(rejectionReason) ? "." : ": " + rejectionReason), "Obtain a new authenticated receipt bound to the exact immutable approval inputs.", prefix + "/authorityReceiptHash");
        }

        private static void ValidateObservedApprovalHash(string stableId, string expected, Dictionary<string, string> observedById, string label, string sourceField, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (observedById == null || !observedById.TryGetValue(stableId, out string observed) || !IsSha256(observed))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, "Ready approval has no observed " + label + " evidence.", "Run the production scanner/evaluator and repeat human approval against observed evidence.", sourceField);
                return;
            }
            if (!string.Equals(expected, observed, StringComparison.OrdinalIgnoreCase))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, "Ready approval " + label + " hash is stale.", "Regenerate evidence and repeat human approval.", sourceField);
        }

        private static void ValidateObservedQualityProfileVersion(string stableId, string expected, Dictionary<string, string> observedById, string sourceField, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (observedById == null || !observedById.TryGetValue(stableId, out string observed) || string.IsNullOrWhiteSpace(observed))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, "Ready approval has no observed quality-profile evidence.", "Run the versioned evaluation profile and repeat human approval.", sourceField);
                return;
            }
            if (!string.Equals(expected, observed, StringComparison.Ordinal))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ApprovalEvidenceInvalid, stableId, string.Empty, "Ready approval is stale for observed VFX quality profile version " + observed + ".", "Re-evaluate and repeat human approval against the observed profile.", sourceField);
        }

        private static void CollectRecipeDependencyIds(VfxAuthoringRecipe recipe, Dictionary<string, VfxAuthoringRecipe> recipes, HashSet<string> output)
        {
            for (int i = 0; i < recipe.IncludedRecipeIds.Count; i++)
            {
                string id = recipe.IncludedRecipeIds[i];
                if (!output.Add(id) || !recipes.TryGetValue(id, out VfxAuthoringRecipe included))
                    continue;
                CollectRecipeDependencyIds(included, recipes, output);
            }
        }

        private static bool AreFallbackContractsCompatible(VfxAuthoringEffect effect, VfxAuthoringEffect fallback)
        {
            return effect.AnchorContract != null && fallback.AnchorContract != null && effect.Lifecycle != null && fallback.Lifecycle != null &&
                   string.Equals(effect.AnchorContract.Anchor, fallback.AnchorContract.Anchor, StringComparison.Ordinal) &&
                   string.Equals(effect.AnchorContract.Space, fallback.AnchorContract.Space, StringComparison.Ordinal) &&
                   string.Equals(effect.Lifecycle.Mode, fallback.Lifecycle.Mode, StringComparison.Ordinal) &&
                   string.Equals(effect.Lifecycle.StopPolicy, fallback.Lifecycle.StopPolicy, StringComparison.Ordinal);
        }

        private static bool IsArtApprovalRole(string value)
        {
            return string.Equals(value, VfxAuthoringApprovalRoles.VfxArtist, StringComparison.Ordinal) ||
                   string.Equals(value, VfxAuthoringApprovalRoles.TechnicalArtist, StringComparison.Ordinal);
        }

        private static bool IsGameplayApprovalRole(string value)
        {
            return string.Equals(value, VfxAuthoringApprovalRoles.Gameplay, StringComparison.Ordinal) ||
                   string.Equals(value, VfxAuthoringApprovalRoles.UserExperience, StringComparison.Ordinal);
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("sha256:", StringComparison.Ordinal) || value.Length != 71)
                return false;
            for (int i = 7; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        private static void AddReadyGateDiagnostics(VfxAuthoringCatalogDocument catalog, List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < catalog.Primitives.Count; i++)
                AddReadyGate(catalog.Primitives[i] != null ? catalog.Primitives[i].PrimitiveId : string.Empty, catalog.Primitives[i] != null ? catalog.Primitives[i].ReviewStatus : string.Empty, diagnostics);
            for (int i = 0; i < catalog.Recipes.Count; i++)
                AddReadyGate(catalog.Recipes[i] != null ? catalog.Recipes[i].RecipeId : string.Empty, catalog.Recipes[i] != null ? catalog.Recipes[i].ReviewStatus : string.Empty, diagnostics);
            for (int i = 0; i < catalog.Effects.Count; i++)
                AddReadyGate(catalog.Effects[i] != null ? catalog.Effects[i].EffectId : string.Empty, catalog.Effects[i] != null ? catalog.Effects[i].ReviewStatus : string.Empty, diagnostics);
        }

        private static void AddReadyGate(string stableId, string reviewStatus, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (!string.Equals(reviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal))
                return;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                AuthoringResourceDiagnostic diagnostic = diagnostics[i];
                if (diagnostic.Severity == CharacterAuthoringValidationSeverity.Error &&
                    string.Equals(diagnostic.SourceStableId, stableId, StringComparison.Ordinal) &&
                    !string.Equals(diagnostic.Code, VfxAuthoringDiagnosticCodes.ReadyGateFailed, StringComparison.Ordinal))
                {
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ReadyGateFailed, stableId, diagnostic.RuntimeResourceKey, "Ready VFX authoring entry has blocking diagnostics.", "Resolve all errors and repeat human review before marking Ready.", "reviewStatus");
                    return;
                }
            }
        }

        private static void ValidateSemanticFields(string stableId, string displayName, string description, string reviewStatus, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (!IsKnownReviewStatus(reviewStatus))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, stableId, string.Empty, "Unknown reviewStatus: " + reviewStatus, "Use Draft, ReviewNeeded, Ready, or Deprecated.", prefix + "/reviewStatus");
            if (string.Equals(reviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal) && (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(description)))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, stableId, string.Empty, "Ready VFX authoring entry requires displayName and description.", "Complete human-facing semantics before Ready approval.", prefix);
        }

        private static void ValidateVisualMaturity(string stableId, string visualMaturity, string reviewStatus, bool exemplar, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (!EqualsAny(visualMaturity, VfxAuthoringVisualMaturities.LegacyPlaceholder, VfxAuthoringVisualMaturities.Candidate, VfxAuthoringVisualMaturities.Production))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.VisualMaturityInvalid, stableId, string.Empty, "Unknown visualMaturity: " + visualMaturity, "Use LegacyPlaceholder, Candidate, or Production.", prefix + "/visualMaturity");
                return;
            }
            if (string.Equals(reviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal) &&
                !string.Equals(visualMaturity, VfxAuthoringVisualMaturities.Production, StringComparison.Ordinal))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.VisualMaturityInvalid, stableId, string.Empty, "Only Production visual content may be marked Ready.", "Complete original asset creation, reference comparison, performance evidence, and human approval before Ready.", prefix + "/visualMaturity");
            if (exemplar && !string.Equals(visualMaturity, VfxAuthoringVisualMaturities.Production, StringComparison.Ordinal))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.VisualMaturityInvalid, stableId, string.Empty, "Only Production visual content may enter the Exemplar Index.", "Remove exemplar=true until Production approval is complete.", prefix + "/exemplar");
        }

        private static void ValidateRoles(string stableId, List<string> roles, string sourceField, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (roles == null || roles.Count == 0)
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, stableId, string.Empty, "VFX authoring entry requires at least one role.", "Add a controlled primary role as the first roles value.", sourceField);
                return;
            }
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < roles.Count; i++)
                if (!IsKnownRole(roles[i]) || !unique.Add(roles[i]))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.SchemaInvalid, stableId, string.Empty, "Unknown or duplicate VFX role: " + roles[i], "Use unique controlled role values.", sourceField + "/" + i);
        }

        private static void ValidateParameters(string stableId, List<VfxAuthoringParameterDefinition> parameters, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var runtimeSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < parameters.Count; i++)
            {
                VfxAuthoringParameterDefinition parameter = parameters[i];
                string field = prefix + "/" + i;
                if (parameter == null || !IsStableSegment(parameter.ParameterId) || !ids.Add(parameter.ParameterId))
                {
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, stableId, string.Empty, "Primitive parameter id is missing, invalid, or duplicated.", "Use one stable parameterId per Primitive.", field + "/parameterId");
                    continue;
                }
                if (!IsKnownParameterValueType(parameter.ValueType) || !IsKnownParameterTransport(parameter.Transport))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, stableId, string.Empty, "Primitive parameter type or transport is invalid: " + parameter.ParameterId, "Use a controlled valueType and transport.", field);
                if (IsRuntimeSlot(parameter.Transport) && !runtimeSlots.Add(parameter.Transport))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, stableId, string.Empty, "Primitive assigns multiple parameters to " + parameter.Transport, "Use each RuntimeSlot0..3 at most once per Primitive.", field + "/transport");
                if (parameter.RuntimeScale <= 0f)
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, stableId, string.Empty, "runtimeScale must be positive for parameter " + parameter.ParameterId, "Set a positive fixed runtime scale.", field + "/runtimeScale");
                if (parameter.DefaultValue.HasValue)
                    ValidateParameterValue(stableId, parameter, parameter.DefaultValue.Value, field + "/defaultValue", diagnostics);
                if (parameter.MinValue.HasValue)
                    ValidateParameterValue(stableId, parameter, parameter.MinValue.Value, field + "/minValue", diagnostics);
                if (parameter.MaxValue.HasValue)
                    ValidateParameterValue(stableId, parameter, parameter.MaxValue.Value, field + "/maxValue", diagnostics);
                if (parameter.MinValue.HasValue && parameter.MaxValue.HasValue &&
                    TryGetComparableNumber(parameter.ValueType, parameter.MinValue.Value, out double minimum) &&
                    TryGetComparableNumber(parameter.ValueType, parameter.MaxValue.Value, out double maximum) && minimum > maximum)
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, stableId, string.Empty, "Parameter minimum exceeds maximum: " + parameter.ParameterId, "Use an ordered numeric range.", field);
            }
        }

        private static void ValidateRecipeParameterContracts(VfxAuthoringRecipe recipe, Dictionary<string, VfxAuthoringPrimitive> primitives, List<AuthoringResourceDiagnostic> diagnostics, string prefix)
        {
            var contracts = new Dictionary<string, VfxAuthoringParameterDefinition>(StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int layerIndex = 0; layerIndex < recipe.Layers.Count; layerIndex++)
            {
                VfxAuthoringRecipeLayer layer = recipe.Layers[layerIndex];
                if (layer == null || !primitives.TryGetValue(layer.PrimitiveId, out VfxAuthoringPrimitive primitive))
                    continue;
                foreach (KeyValuePair<string, string> binding in layer.Bindings)
                {
                    VfxAuthoringParameterDefinition parameter = FindParameter(primitive.Parameters, binding.Key);
                    if (parameter == null || !recipe.Parameters.Contains(binding.Value))
                        continue;
                    used.Add(binding.Value);
                    if (contracts.TryGetValue(binding.Value, out VfxAuthoringParameterDefinition current) &&
                        !string.Equals(current.ValueType, parameter.ValueType, StringComparison.Ordinal))
                        Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, recipe.RecipeId, string.Empty, "Recipe parameter binds incompatible Primitive value types: " + binding.Value + " (" + current.ValueType + " and " + parameter.ValueType + ").", "Bind one Recipe parameter only to compatible Primitive parameter contracts.", prefix + "/layers/" + layerIndex + "/bindings/" + binding.Key);
                    else if (!contracts.ContainsKey(binding.Value))
                        contracts.Add(binding.Value, parameter);
                }
            }
            for (int i = 0; i < recipe.Parameters.Count; i++)
                if (!used.Contains(recipe.Parameters[i]))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, recipe.RecipeId, string.Empty, "Recipe parameter has no Primitive binding: " + recipe.Parameters[i], "Bind the Recipe parameter to at least one Primitive parameter or remove it.", prefix + "/parameters/" + i);
        }

        private static Dictionary<string, VfxAuthoringParameterDefinition> BuildRecipeParameterContracts(VfxAuthoringRecipe recipe, Dictionary<string, VfxAuthoringPrimitive> primitives)
        {
            var result = new Dictionary<string, VfxAuthoringParameterDefinition>(StringComparer.Ordinal);
            for (int layerIndex = 0; layerIndex < recipe.Layers.Count; layerIndex++)
            {
                VfxAuthoringRecipeLayer layer = recipe.Layers[layerIndex];
                if (layer == null || !primitives.TryGetValue(layer.PrimitiveId, out VfxAuthoringPrimitive primitive))
                    continue;
                foreach (KeyValuePair<string, string> binding in layer.Bindings)
                {
                    VfxAuthoringParameterDefinition parameter = FindParameter(primitive.Parameters, binding.Key);
                    if (parameter != null && recipe.Parameters.Contains(binding.Value) && !result.ContainsKey(binding.Value))
                        result.Add(binding.Value, parameter);
                }
            }
            return result;
        }

        private static VfxAuthoringParameterDefinition FindParameter(List<VfxAuthoringParameterDefinition> parameters, string parameterId)
        {
            for (int i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && string.Equals(parameters[i].ParameterId, parameterId, StringComparison.Ordinal))
                    return parameters[i];
            return null;
        }

        private static void ValidateParameterValue(string stableId, VfxAuthoringParameterDefinition parameter, JsonElement value, string sourceField, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (!IsParameterValueCompatible(parameter.ValueType, value))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, stableId, string.Empty, "Parameter value type does not match " + parameter.ValueType + ": " + parameter.ParameterId, "Use a value matching the Primitive parameter contract.", sourceField);
                return;
            }
            if (!TryGetComparableNumber(parameter.ValueType, value, out double number))
                return;
            if (parameter.MinValue.HasValue && TryGetComparableNumber(parameter.ValueType, parameter.MinValue.Value, out double minimum) && number < minimum)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, stableId, string.Empty, "Parameter value is below the reviewed minimum: " + parameter.ParameterId, "Use a value inside the Primitive parameter range.", sourceField);
            if (parameter.MaxValue.HasValue && TryGetComparableNumber(parameter.ValueType, parameter.MaxValue.Value, out double maximum) && number > maximum)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxAuthoringDiagnosticCodes.ParameterInvalid, stableId, string.Empty, "Parameter value exceeds the reviewed maximum: " + parameter.ParameterId, "Use a value inside the Primitive parameter range.", sourceField);
        }

        private static bool IsParameterValueCompatible(string valueType, JsonElement value)
        {
            if (string.Equals(valueType, VfxAuthoringParameterValueTypes.Float, StringComparison.Ordinal))
                return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out _);
            if (string.Equals(valueType, VfxAuthoringParameterValueTypes.Int, StringComparison.Ordinal) || string.Equals(valueType, VfxAuthoringParameterValueTypes.Event, StringComparison.Ordinal))
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _);
            if (string.Equals(valueType, VfxAuthoringParameterValueTypes.Bool, StringComparison.Ordinal))
                return value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False;
            if (string.Equals(valueType, VfxAuthoringParameterValueTypes.Vector3, StringComparison.Ordinal))
                return IsNumericArray(value, 3, 3);
            if (string.Equals(valueType, VfxAuthoringParameterValueTypes.Color, StringComparison.Ordinal))
                return IsNumericArray(value, 3, 4);
            return false;
        }

        private static bool IsNumericArray(JsonElement value, int minimumCount, int maximumCount)
        {
            if (value.ValueKind != JsonValueKind.Array)
                return false;
            int count = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out _))
                    return false;
                count++;
            }
            return count >= minimumCount && count <= maximumCount;
        }

        private static bool TryGetComparableNumber(string valueType, JsonElement value, out double number)
        {
            number = 0d;
            if (string.Equals(valueType, VfxAuthoringParameterValueTypes.Float, StringComparison.Ordinal))
                return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out number);
            if ((string.Equals(valueType, VfxAuthoringParameterValueTypes.Int, StringComparison.Ordinal) || string.Equals(valueType, VfxAuthoringParameterValueTypes.Event, StringComparison.Ordinal)) &&
                value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long integer))
            {
                number = integer;
                return true;
            }
            return false;
        }

        private static void CollectRecipePrimitives(VfxAuthoringRecipe recipe, Dictionary<string, VfxAuthoringPrimitive> primitives, Dictionary<string, VfxAuthoringRecipe> recipes, HashSet<string> visitedRecipes, List<VfxAuthoringPrimitive> output)
        {
            if (recipe == null || !visitedRecipes.Add(recipe.RecipeId))
                return;
            for (int i = 0; i < recipe.Layers.Count; i++)
                if (recipe.Layers[i] != null && primitives.TryGetValue(recipe.Layers[i].PrimitiveId, out VfxAuthoringPrimitive primitive) && !ContainsPrimitive(output, primitive.PrimitiveId))
                    output.Add(primitive);
            for (int i = 0; i < recipe.IncludedRecipeIds.Count; i++)
                if (recipes.TryGetValue(recipe.IncludedRecipeIds[i], out VfxAuthoringRecipe included))
                    CollectRecipePrimitives(included, primitives, recipes, visitedRecipes, output);
        }

        private static bool ContainsPrimitive(List<VfxAuthoringPrimitive> values, string primitiveId)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i].PrimitiveId, primitiveId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static CharacterAuthoringValidationSeverity ReadySensitiveSeverity(VfxAuthoringEffect effect)
        {
            return string.Equals(effect.ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal)
                ? CharacterAuthoringValidationSeverity.Error
                : CharacterAuthoringValidationSeverity.Warning;
        }

        private static HashSet<string> ToUniqueSet(List<string> values, string stableId, string sourceField, string code, List<AuthoringResourceDiagnostic> diagnostics)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
                if (!IsStableSegment(values[i]) || !result.Add(values[i]))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, code, stableId, string.Empty, "Authoring parameter id is missing, invalid, or duplicated: " + values[i], "Use unique stable parameter ids.", sourceField + "/" + i);
            return result;
        }

        private static HashSet<string> ParameterIds(List<VfxAuthoringParameterDefinition> parameters)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && !string.IsNullOrWhiteSpace(parameters[i].ParameterId))
                    result.Add(parameters[i].ParameterId);
            return result;
        }

        private static void Add(List<AuthoringResourceDiagnostic> diagnostics, CharacterAuthoringValidationSeverity severity, string code, string stableId, string runtimeResourceKey, string message, string suggestedFix, string sourceField)
        {
            diagnostics.Add(CreateDiagnostic(severity, code, stableId, runtimeResourceKey, message, suggestedFix, sourceField));
        }

        private static bool HasErrors(List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
                if (diagnostics[i].Severity == CharacterAuthoringValidationSeverity.Error)
                    return true;
            return false;
        }

        private static int CompareDiagnostics(AuthoringResourceDiagnostic left, AuthoringResourceDiagnostic right)
        {
            int value = string.Compare(left != null ? left.Code : string.Empty, right != null ? right.Code : string.Empty, StringComparison.Ordinal);
            if (value != 0) return value;
            value = string.Compare(left != null ? left.SourceStableId : string.Empty, right != null ? right.SourceStableId : string.Empty, StringComparison.Ordinal);
            if (value != 0) return value;
            value = string.Compare(left != null ? left.SourceField : string.Empty, right != null ? right.SourceField : string.Empty, StringComparison.Ordinal);
            if (value != 0) return value;
            return string.Compare(left != null ? left.RuntimeResourceKey : string.Empty, right != null ? right.RuntimeResourceKey : string.Empty, StringComparison.Ordinal);
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-'))
                    return false;
            }
            return true;
        }

        private static bool IsStableSegment(string value) => IsStableId(value) && value.IndexOf('.', StringComparison.Ordinal) < 0;
        private static bool IsKnownBackend(string value) => EqualsAny(value, VfxAuthoringBackends.ParticleSystem, VfxAuthoringBackends.LineRenderer, VfxAuthoringBackends.CustomView, VfxAuthoringBackends.VfxGraph);
        private static bool IsKnownRole(string value) => EqualsAny(value, VfxAuthoringRoles.Charge, VfxAuthoringRoles.Cast, VfxAuthoringRoles.Flight, VfxAuthoringRoles.Impact, VfxAuthoringRoles.Telegraph, VfxAuthoringRoles.Beam, VfxAuthoringRoles.Aura, VfxAuthoringRoles.Status, VfxAuthoringRoles.Zone, VfxAuthoringRoles.Environment, VfxAuthoringRoles.Transition);
        private static bool IsKnownAnchor(string value) => EqualsAny(value, VfxAuthoringAnchors.World, VfxAuthoringAnchors.Caster, VfxAuthoringAnchors.Target, VfxAuthoringAnchors.Socket, VfxAuthoringAnchors.HitPoint, VfxAuthoringAnchors.Path, VfxAuthoringAnchors.Ground);
        private static bool IsKnownSpace(string value) => EqualsAny(value, VfxAuthoringSpaces.Local, VfxAuthoringSpaces.World);
        private static bool IsKnownLifecycle(string value) => EqualsAny(value, VfxAuthoringLifecycleModes.OneShot, VfxAuthoringLifecycleModes.Loop, VfxAuthoringLifecycleModes.Persistent, VfxAuthoringLifecycleModes.ManualStop);
        private static bool IsKnownStopPolicy(string value) => EqualsAny(value, VfxAuthoringStopPolicies.Expire, VfxAuthoringStopPolicies.Stop, VfxAuthoringStopPolicies.Manual);
        private static bool IsKnownReviewStatus(string value) => EqualsAny(value, VfxAuthoringReviewStatuses.Draft, VfxAuthoringReviewStatuses.ReviewNeeded, VfxAuthoringReviewStatuses.Ready, VfxAuthoringReviewStatuses.Deprecated);
        private static bool IsKnownPriority(string value) => EqualsAny(value, VfxAuthoringPriorities.Cosmetic, VfxAuthoringPriorities.Normal, VfxAuthoringPriorities.GameplayImportant, VfxAuthoringPriorities.CriticalTelegraph);
        private static bool IsKnownCostTier(string value) => EqualsAny(value, VfxAuthoringCostTiers.Low, VfxAuthoringCostTiers.Medium, VfxAuthoringCostTiers.High);
        private static bool IsKnownFallbackPolicy(string value) => EqualsAny(value, VfxAuthoringFallbackPolicies.None, VfxAuthoringFallbackPolicies.UseResourceVariant, VfxAuthoringFallbackPolicies.UseFallbackEffect);
        private static bool IsKnownParameterValueType(string value) => EqualsAny(value, VfxAuthoringParameterValueTypes.Float, VfxAuthoringParameterValueTypes.Int, VfxAuthoringParameterValueTypes.Bool, VfxAuthoringParameterValueTypes.Color, VfxAuthoringParameterValueTypes.Vector3, VfxAuthoringParameterValueTypes.Event);
        private static bool IsKnownParameterTransport(string value) => EqualsAny(value, VfxAuthoringParameterTransports.RuntimeSlot0, VfxAuthoringParameterTransports.RuntimeSlot1, VfxAuthoringParameterTransports.RuntimeSlot2, VfxAuthoringParameterTransports.RuntimeSlot3, VfxAuthoringParameterTransports.StaticBake, VfxAuthoringParameterTransports.PreviewOnly, VfxAuthoringParameterTransports.BackendDefault);
        private static bool IsRuntimeSlot(string value) => EqualsAny(value, VfxAuthoringParameterTransports.RuntimeSlot0, VfxAuthoringParameterTransports.RuntimeSlot1, VfxAuthoringParameterTransports.RuntimeSlot2, VfxAuthoringParameterTransports.RuntimeSlot3);

        private static bool EqualsAny(string value, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
                if (string.Equals(value, candidates[i], StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
