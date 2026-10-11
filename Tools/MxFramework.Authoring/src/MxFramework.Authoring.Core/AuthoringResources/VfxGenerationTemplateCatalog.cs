using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>Creates the versioned baseline quality profile used by the first Fireball vertical slice.</summary>
    public static class VfxQualityProfileDefaults
    {
        public static readonly string[] RequiredBaselineHardGateIds =
        {
            VfxHardGateIds.NoConsoleOrRenderErrors,
            VfxHardGateIds.NoMagentaMaterial,
            VfxHardGateIds.NoNanOrMissingFrames,
            VfxHardGateIds.CropWithinLimit,
            VfxHardGateIds.AnchorWithinLimit,
            VfxHardGateIds.PathEndpointWithinLimit,
            VfxHardGateIds.TelegraphFootprint,
            VfxHardGateIds.CleanupWithinOneSecond,
            VfxHardGateIds.NoGhostAfterTwentyCycles,
            VfxHardGateIds.FallbackTimingAndRange,
            VfxHardGateIds.PerformanceProfilePassed,
            VfxHardGateIds.ZeroSteadyStateGc
        };

        /// <summary>Creates the Windows/D3D12/1080p60 profile with all required deterministic capture and budget data.</summary>
        /// <remarks>AllocByDesign. Returns an independent mutable authoring DTO.</remarks>
        public static VfxQualityProfile CreateWindowsD3D12_1080p60(string version = "1")
        {
            var profile = new VfxQualityProfile
            {
                QualityProfileId = "windows.d3d12.1080p60",
                Version = string.IsNullOrWhiteSpace(version) ? "1" : version.Trim()
            };
            profile.RequiredHardGateIds.AddRange(RequiredBaselineHardGateIds);
            return profile;
        }
    }

    /// <summary>Normalizes and validates generation-template catalogs without touching Unity assets.</summary>
    public static class VfxGenerationTemplateCatalog
    {
        /// <summary>Canonicalizes set-like lists and identity ordering.</summary>
        /// <remarks>LowFreqAlloc. Mutates the supplied Authoring DTO.</remarks>
        public static void NormalizeInPlace(VfxGenerationTemplateCatalogDocument catalog)
        {
            if (catalog == null)
                return;
            catalog.SchemaVersion = Trim(catalog.SchemaVersion);
            catalog.CatalogId = Trim(catalog.CatalogId);
            catalog.CatalogVersion = Trim(catalog.CatalogVersion);
            catalog.Providers ??= new List<VfxGenerationProviderDefinition>();
            catalog.StyleProfiles ??= new List<VfxStyleProfile>();
            catalog.Templates ??= new List<VfxGenerationTemplateDefinition>();
            catalog.QualityProfiles ??= new List<VfxQualityProfile>();

            for (int i = 0; i < catalog.Providers.Count; i++)
            {
                VfxGenerationProviderDefinition provider = catalog.Providers[i];
                if (provider == null) continue;
                provider.ProviderId = Trim(provider.ProviderId);
                provider.ProviderVersion = Trim(provider.ProviderVersion);
                provider.Kind = Trim(provider.Kind);
                provider.ModelId = Trim(provider.ModelId);
                provider.ModelVersion = Trim(provider.ModelVersion);
                provider.AllowedLicenseIds ??= new List<string>();
                NormalizeStrings(provider.AllowedLicenseIds);
            }
            for (int i = 0; i < catalog.StyleProfiles.Count; i++)
            {
                VfxStyleProfile style = catalog.StyleProfiles[i];
                if (style == null) continue;
                style.StyleProfileId = Trim(style.StyleProfileId);
                style.Version = Trim(style.Version);
                style.ThemeIds ??= new List<string>();
                style.Keywords ??= new List<string>();
                style.ExemplarIds ??= new List<string>();
                NormalizeStrings(style.ThemeIds);
                NormalizeStrings(style.Keywords);
                NormalizeStrings(style.ExemplarIds);
            }
            for (int i = 0; i < catalog.Templates.Count; i++)
            {
                VfxGenerationTemplateDefinition template = catalog.Templates[i];
                if (template == null) continue;
                template.TemplateId = Trim(template.TemplateId);
                template.TemplateVersion = Trim(template.TemplateVersion);
                template.Kind = Trim(template.Kind);
                template.Backend = Trim(template.Backend);
                template.AssetPath = VfxCandidatePathPolicy.NormalizeProjectPath(template.AssetPath);
                template.ExperimentalAdapterId = Trim(template.ExperimentalAdapterId);
                template.ExperimentalAdapterVersion = Trim(template.ExperimentalAdapterVersion);
                template.Roles ??= new List<string>();
                template.CompatibleAnchors ??= new List<string>();
                template.AllowedProviderIds ??= new List<string>();
                template.ParameterContracts ??= new List<VfxAuthoringParameterDefinition>();
                template.BudgetRange ??= new VfxGenerationBudgetRange();
                template.ExemplarIds ??= new List<string>();
                NormalizeStrings(template.Roles);
                NormalizeStrings(template.CompatibleAnchors);
                NormalizeStrings(template.AllowedProviderIds);
                NormalizeStrings(template.ExemplarIds);
                template.ParameterContracts.Sort((left, right) => Compare(left != null ? left.ParameterId : string.Empty, right != null ? right.ParameterId : string.Empty));
            }
            for (int i = 0; i < catalog.QualityProfiles.Count; i++)
                NormalizeQualityProfile(catalog.QualityProfiles[i]);

            catalog.Providers.Sort((left, right) => Compare(left != null ? left.ProviderId : string.Empty, right != null ? right.ProviderId : string.Empty));
            catalog.StyleProfiles.Sort((left, right) => Compare(left != null ? left.StyleProfileId : string.Empty, right != null ? right.StyleProfileId : string.Empty));
            catalog.Templates.Sort((left, right) => Compare(left != null ? left.TemplateId : string.Empty, right != null ? right.TemplateId : string.Empty));
            catalog.QualityProfiles.Sort((left, right) => Compare(left != null ? left.QualityProfileId : string.Empty, right != null ? right.QualityProfileId : string.Empty));
        }

        /// <summary>Validates controlled providers, certified templates, quality thresholds, and Ready exemplar references.</summary>
        /// <remarks>LowFreqAlloc. Builds identity sets and stable diagnostics.</remarks>
        public static VfxGenerationTemplateCatalogValidationReport Validate(VfxGenerationTemplateCatalogDocument catalog, VfxAuthoringCatalogDocument authoringCatalog = null)
        {
            var report = new VfxGenerationTemplateCatalogValidationReport();
            if (catalog == null)
            {
                Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, string.Empty, "Generation template catalog is missing.", "Provide " + VfxCreativeFlywheelSchemaVersions.GenerationTemplatesV1 + ".", "generationTemplates");
                report.HasErrors = true;
                return report;
            }
            NormalizeInPlace(catalog);
            if (!string.Equals(catalog.SchemaVersion, VfxCreativeFlywheelSchemaVersions.GenerationTemplatesV1, StringComparison.Ordinal) || !IsStableId(catalog.CatalogId) || string.IsNullOrWhiteSpace(catalog.CatalogVersion))
                Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, catalog.CatalogId, "Generation template catalog header is invalid.", "Use the supported schema plus stable catalog id/version.", "generationTemplates");

            var providers = new Dictionary<string, VfxGenerationProviderDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Providers.Count; i++)
            {
                VfxGenerationProviderDefinition provider = catalog.Providers[i];
                string id = provider != null ? provider.ProviderId : string.Empty;
                if (provider == null || !IsStableId(id) || !providers.TryAdd(id, provider) || string.IsNullOrWhiteSpace(provider.ProviderVersion) || !IsProviderKind(provider.Kind))
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Generation provider identity, version, or kind is invalid/duplicated.", "Use one stable provider definition with an explicit version.", "providers/" + i);
                if (provider != null && string.Equals(provider.Kind, VfxGenerationProviderKinds.Cloud, StringComparison.Ordinal) && provider.EnabledByDefault)
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Cloud generation providers must be disabled by default.", "Set enabledByDefault=false and enable cloud access explicitly outside the repository.", "providers/" + i + "/enabledByDefault");
                if (provider != null && (string.IsNullOrWhiteSpace(provider.ModelId) != string.IsNullOrWhiteSpace(provider.ModelVersion)))
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Generation provider model identity/version must be declared as an exact pair.", "Set both modelId and modelVersion, or leave both empty for a disabled unusable placeholder.", "providers/" + i + "/modelVersion");
                if (provider != null && IsExternalModelProvider(provider.Kind) && provider.EnabledByDefault && !HasExactModelBinding(provider))
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Enabled local/cloud generation provider lacks an exact model id/version.", "Pin the exact model id/version before enabling the provider.", "providers/" + i + "/modelId");
            }

            var styles = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.StyleProfiles.Count; i++)
            {
                VfxStyleProfile style = catalog.StyleProfiles[i];
                string id = style != null ? style.StyleProfileId : string.Empty;
                if (style == null || !IsStableId(id) || !styles.Add(id) || string.IsNullOrWhiteSpace(style.Version))
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Style Profile identity/version is invalid or duplicated.", "Use one stable, versioned Style Profile.", "styleProfiles/" + i);
                if (style != null)
                    ValidateExemplars(style.ExemplarIds, authoringCatalog, id, "styleProfiles/" + i + "/exemplarIds", report.Diagnostics);
            }

            var templates = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Templates.Count; i++)
            {
                VfxGenerationTemplateDefinition template = catalog.Templates[i];
                string id = template != null ? template.TemplateId : string.Empty;
                if (template == null || !IsStableId(id) || !templates.Add(id) || string.IsNullOrWhiteSpace(template.TemplateVersion) || !IsTemplateKind(template.Kind) || !IsBackend(template.Backend))
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Generation template identity/version/kind/backend is invalid or duplicated.", "Use one stable versioned controlled template.", "templates/" + i);
                if (template == null) continue;
                if (!VfxCandidatePathPolicy.IsFormalDestination(template.AssetPath))
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SandboxViolation, id, "Certified template asset path must be a formal project asset outside candidate sandboxes.", "Use a project-relative Assets/VFX path.", "templates/" + i + "/assetPath");
                if (template.Certified && template.ArbitraryTopology)
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.ExperimentalDraftOnly, id, "Arbitrary Graph topology cannot be certified for production generation.", "Use certified templates/subgraphs/exposed properties, or keep the adapter experimental and Draft-only.", "templates/" + i);
                if (template.ArbitraryTopology && (!string.Equals(template.ExperimentalAdapterId, "unity-vfxgraph-17.5-experimental", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(template.ExperimentalAdapterVersion)))
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.ExperimentalDraftOnly, id, "Arbitrary Graph topology requires the version-locked unity-vfxgraph-17.5-experimental adapter.", "Declare the exact experimental adapter id/version; it remains Draft-only.", "templates/" + i + "/experimentalAdapterId");
                for (int providerIndex = 0; providerIndex < template.AllowedProviderIds.Count; providerIndex++)
                    if (!providers.ContainsKey(template.AllowedProviderIds[providerIndex]))
                        Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Template references an unknown provider: " + template.AllowedProviderIds[providerIndex], "Add the provider or remove the reference.", "templates/" + i + "/allowedProviderIds/" + providerIndex);
                if (template.Roles.Count == 0 || template.CompatibleAnchors.Count == 0)
                    Add(report.Diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Generation template requires role and Anchor compatibility.", "Declare controlled roles and compatible Anchors.", "templates/" + i);
                ValidateExemplars(template.ExemplarIds, authoringCatalog, id, "templates/" + i + "/exemplarIds", report.Diagnostics);
            }

            var qualityIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.QualityProfiles.Count; i++)
                ValidateQualityProfile(catalog.QualityProfiles[i], i, qualityIds, report.Diagnostics);

            report.Diagnostics.Sort(CompareDiagnostics);
            report.HasErrors = VfxCreativeFlywheelUtilities.HasErrors(report.Diagnostics);
            return report;
        }

        /// <summary>Computes the normalized semantic hash used by generation plans.</summary>
        /// <remarks>AllocByDesign. Normalizes the supplied catalog then hashes canonical JSON.</remarks>
        public static string ComputeHash(VfxGenerationTemplateCatalogDocument catalog)
        {
            NormalizeInPlace(catalog);
            return VfxCreativeFlywheelHash.Compute(catalog);
        }

        private static void NormalizeQualityProfile(VfxQualityProfile profile)
        {
            if (profile == null) return;
            profile.QualityProfileId = Trim(profile.QualityProfileId);
            profile.Version = Trim(profile.Version);
            profile.Platform = Trim(profile.Platform);
            profile.GraphicsApi = Trim(profile.GraphicsApi);
            profile.FixedSeeds ??= new List<int>();
            profile.OneShotSlicePercents ??= new List<int>();
            profile.CaptureViewIds ??= new List<string>();
            profile.CaptureBackgroundIds ??= new List<string>();
            profile.PerformanceTierBudgets ??= new List<VfxPerformanceTierBudget>();
            profile.RequiredHardGateIds ??= new List<string>();
            profile.FixedSeeds.Sort();
            profile.OneShotSlicePercents.Sort();
            NormalizeStrings(profile.CaptureViewIds);
            NormalizeStrings(profile.CaptureBackgroundIds);
            NormalizeStrings(profile.RequiredHardGateIds);
            for (int i = 0; i < profile.PerformanceTierBudgets.Count; i++)
                if (profile.PerformanceTierBudgets[i] != null)
                    profile.PerformanceTierBudgets[i].CostTier = Trim(profile.PerformanceTierBudgets[i].CostTier);
            profile.PerformanceTierBudgets.Sort((left, right) => Compare(left != null ? left.CostTier : string.Empty, right != null ? right.CostTier : string.Empty));
        }

        private static void ValidateQualityProfile(VfxQualityProfile profile, int index, HashSet<string> ids, List<AuthoringResourceDiagnostic> diagnostics)
        {
            string prefix = "qualityProfiles/" + index;
            string id = profile != null ? profile.QualityProfileId : string.Empty;
            if (profile == null || !IsStableId(id) || !ids.Add(id) || string.IsNullOrWhiteSpace(profile.Version))
            {
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Quality Profile identity/version is invalid or duplicated.", "Use one stable versioned profile.", prefix);
                return;
            }
            bool fixedStepValid = Math.Abs(profile.FixedStepSeconds - (1d / 60d)) < 0.0000001d;
            if (!fixedStepValid || !SequenceEqual(profile.FixedSeeds, 17, 73, 211) || !SequenceEqual(profile.OneShotSlicePercents, 0, 10, 25, 50, 75, 100) ||
                profile.LoopWarmupSeconds <= 0f || profile.LoopSteadySeconds <= 0f || profile.LoopCleanupSeconds < 1f ||
                profile.CaptureViewIds.Count < 3 || profile.CaptureBackgroundIds.Count < 4 || profile.GhostCycleCount < 20 ||
                profile.CriticalTelegraphContrastRatio < 3f || profile.GameplayImportantContrastRatio < 2f ||
                profile.MaximumSteadyStateGcBytesPerFrame != 0 || profile.PerformanceWarmupFrames < 120 || profile.PerformanceSampleFrames < 600)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Quality Profile omits or weakens deterministic capture, cleanup, contrast, ghost, GC, or sample requirements.", "Use the versioned 1/60, 17/73/211, required capture matrix, 20-cycle ghost, zero-GC, 120/600-frame baseline.", prefix);
            if (profile.MaximumCropPercent > 0.5f || profile.MaximumAnchorErrorPercent > 5f || profile.MaximumPathEndpointErrorPercent > 2f ||
                profile.MinimumTelegraphFootprintIou < 0.90f || profile.MaximumFallbackPeakTimeDeltaPercent > 10f || profile.MaximumFallbackVisibleRangeDeltaPercent > 15f)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Quality Profile weakens a fixed VFX spatial or fallback gate.", "Keep crop/Anchor/path/IoU/fallback thresholds at least as strict as the reviewed baseline.", prefix);
            if (profile.MinimumTotalScore < 80 || profile.MinimumDimensionScore < 60 || profile.MaximumReviewerSpread > 15 || profile.MaximumRounds > 3 || profile.MinimumRoundImprovement < 3)
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Quality Profile weakens shortlist or evolution thresholds.", "Use total>=80, dimension>=60, spread<=15, rounds<=3, and improvement>=3.", prefix);
            ValidateTierBudgets(profile, prefix, diagnostics);
            for (int gateIndex = 0; gateIndex < VfxQualityProfileDefaults.RequiredBaselineHardGateIds.Length; gateIndex++)
                if (!profile.RequiredHardGateIds.Contains(VfxQualityProfileDefaults.RequiredBaselineHardGateIds[gateIndex]))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, id, "Quality Profile is missing required hard gate: " + VfxQualityProfileDefaults.RequiredBaselineHardGateIds[gateIndex], "Add the complete versioned spatial, lifecycle, fallback, render, and performance gate set.", prefix + "/requiredHardGateIds");
        }

        private static void ValidateTierBudgets(VfxQualityProfile profile, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            VfxPerformanceTierBudget low = FindTier(profile.PerformanceTierBudgets, VfxAuthoringCostTiers.Low);
            VfxPerformanceTierBudget medium = FindTier(profile.PerformanceTierBudgets, VfxAuthoringCostTiers.Medium);
            VfxPerformanceTierBudget high = FindTier(profile.PerformanceTierBudgets, VfxAuthoringCostTiers.High);
            if (!Within(low, 0.25f, 0.5f) || !Within(medium, 0.5f, 1f) || !Within(high, 1f, 2f))
                Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, profile.QualityProfileId, "Per-cost-tier CPU/GPU P95 budgets are missing or weaker than the baseline.", "Use Low 0.25/0.5 ms, Medium 0.5/1 ms, High 1/2 ms or stricter.", prefix + "/performanceTierBudgets");
        }

        private static VfxPerformanceTierBudget FindTier(List<VfxPerformanceTierBudget> values, string tier)
        {
            for (int i = 0; i < values.Count; i++)
                if (values[i] != null && string.Equals(values[i].CostTier, tier, StringComparison.Ordinal))
                    return values[i];
            return null;
        }

        private static bool Within(VfxPerformanceTierBudget value, float cpu, float gpu) => value != null && value.CpuP95Milliseconds > 0f && value.CpuP95Milliseconds <= cpu && value.GpuP95Milliseconds > 0f && value.GpuP95Milliseconds <= gpu;

        private static void ValidateExemplars(List<string> exemplarIds, VfxAuthoringCatalogDocument catalog, string ownerId, string prefix, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (catalog == null)
                return;
            for (int i = 0; i < exemplarIds.Count; i++)
                if (!IsReadyExemplar(catalog, exemplarIds[i]))
                    Add(diagnostics, CharacterAuthoringValidationSeverity.Error, VfxFlywheelDiagnosticCodes.SchemaInvalid, ownerId, "Positive exemplar must reference a Ready entry explicitly marked exemplar: " + exemplarIds[i], "Complete human Ready approval and set exemplar=true, or remove the reference.", prefix + "/" + i);
        }

        private static bool IsReadyExemplar(VfxAuthoringCatalogDocument catalog, string id)
        {
            for (int i = 0; i < catalog.Primitives.Count; i++)
                if (catalog.Primitives[i] != null && string.Equals(catalog.Primitives[i].PrimitiveId, id, StringComparison.Ordinal))
                    return catalog.Primitives[i].Exemplar && string.Equals(catalog.Primitives[i].ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal);
            for (int i = 0; i < catalog.Recipes.Count; i++)
                if (catalog.Recipes[i] != null && string.Equals(catalog.Recipes[i].RecipeId, id, StringComparison.Ordinal))
                    return catalog.Recipes[i].Exemplar && string.Equals(catalog.Recipes[i].ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal);
            for (int i = 0; i < catalog.Effects.Count; i++)
                if (catalog.Effects[i] != null && string.Equals(catalog.Effects[i].EffectId, id, StringComparison.Ordinal))
                    return catalog.Effects[i].Exemplar && string.Equals(catalog.Effects[i].ReviewStatus, VfxAuthoringReviewStatuses.Ready, StringComparison.Ordinal);
            return false;
        }

        private static bool SequenceEqual(List<int> values, params int[] expected)
        {
            if (values == null || values.Count != expected.Length) return false;
            for (int i = 0; i < expected.Length; i++) if (values[i] != expected[i]) return false;
            return true;
        }

        private static bool IsProviderKind(string value) => EqualsAny(value, VfxGenerationProviderKinds.DevelopmentAgent, VfxGenerationProviderKinds.BuiltInProcedural, VfxGenerationProviderKinds.LocalCli, VfxGenerationProviderKinds.LocalHttp, VfxGenerationProviderKinds.Cloud, VfxGenerationProviderKinds.UnityTemplate, VfxGenerationProviderKinds.ExperimentalUnityGraph);
        internal static bool IsExternalModelProvider(string value) => EqualsAny(value, VfxGenerationProviderKinds.LocalCli, VfxGenerationProviderKinds.LocalHttp, VfxGenerationProviderKinds.Cloud);
        internal static bool HasExactModelBinding(VfxGenerationProviderDefinition provider) => provider != null && !string.IsNullOrWhiteSpace(provider.ModelId) && !string.IsNullOrWhiteSpace(provider.ModelVersion);
        private static bool IsTemplateKind(string value) => EqualsAny(value, VfxGenerationTemplateKinds.ParticleSystemPrimitive, VfxGenerationTemplateKinds.LineRendererPrimitive, VfxGenerationTemplateKinds.VfxGraph, VfxGenerationTemplateKinds.ShaderGraph, VfxGenerationTemplateKinds.VfxSystemSubgraph, VfxGenerationTemplateKinds.VfxBlockSubgraph, VfxGenerationTemplateKinds.VfxOperatorSubgraph);
        private static bool IsBackend(string value) => EqualsAny(value, VfxAuthoringBackends.ParticleSystem, VfxAuthoringBackends.LineRenderer, VfxAuthoringBackends.VfxGraph, VfxAuthoringBackends.CustomView);

        private static bool EqualsAny(string value, params string[] expected)
        {
            for (int i = 0; i < expected.Length; i++) if (string.Equals(value, expected[i], StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')) return false;
            }
            return true;
        }

        private static void NormalizeStrings(List<string> values)
        {
            for (int i = values.Count - 1; i >= 0; i--)
            {
                values[i] = Trim(values[i]);
                if (values[i].Length == 0) values.RemoveAt(i);
            }
            values.Sort(Compare);
        }

        private static void Add(List<AuthoringResourceDiagnostic> diagnostics, CharacterAuthoringValidationSeverity severity, string code, string stableId, string message, string fix, string field)
        {
            diagnostics.Add(VfxAuthoringCatalogValidator.CreateDiagnostic(severity, code, stableId, string.Empty, message, fix, field));
        }

        private static int CompareDiagnostics(AuthoringResourceDiagnostic left, AuthoringResourceDiagnostic right)
        {
            int value = Compare(left != null ? left.Code : string.Empty, right != null ? right.Code : string.Empty);
            if (value != 0) return value;
            value = Compare(left != null ? left.SourceStableId : string.Empty, right != null ? right.SourceStableId : string.Empty);
            if (value != 0) return value;
            return Compare(left != null ? left.SourceField : string.Empty, right != null ? right.SourceField : string.Empty);
        }

        private static string Trim(string value) => value == null ? string.Empty : value.Trim();
        private static int Compare(string left, string right) => string.Compare(left, right, StringComparison.Ordinal);
    }
}
