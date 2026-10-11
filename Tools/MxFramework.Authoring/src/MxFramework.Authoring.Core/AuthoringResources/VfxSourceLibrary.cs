using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MxFramework.Authoring
{
    public static class VfxSourceLibrarySchemas
    {
        public const string IndexV1 = "mx.vfx.source-library.v1";
        public const string CurationV1 = "mx.vfx.source-curation.v1";
        public const string ExtractionPlanV1 = "mx.vfx.source-extraction-plan.v1";
        public const string ExtractionReceiptV1 = "mx.vfx.source-extraction-receipt.v1";
        public const string CompositionPlanV1 = "mx.vfx.source-composition-plan.v1";
        public const string ElementIndexV1 = "mx.vfx.source-element-index.v1";
        public const string ExtractionRegistryV1 = "mx.vfx.source-extraction-registry.v1";
    }

    public static class VfxSourceDependencyPolicies
    {
        public const string DirectReference = "DirectReference";
        public const string DerivedWhenModified = "DerivedWhenModified";
    }

    public static class VfxSourceComponentKinds
    {
        public const string ParticleSystemLayer = "ParticleSystemLayer";
        public const string ResponsibilityGroup = "ResponsibilityGroup";
        public const string CompoundPrimitive = "CompoundPrimitive";
    }

    /// <summary>Rebuildable, path-stable index of an authorized VFX source library.</summary>
    /// <remarks>AllocByDesign. Authoring-only and never consumed by Runtime.</remarks>
    public sealed class VfxSourceLibraryDocument
    {
        public string SchemaVersion { get; set; } = VfxSourceLibrarySchemas.IndexV1;
        public string SourceLibraryId { get; set; } = "source.piloto.studio";
        public string SourceRoot { get; set; } = "Assets/Piloto Studio";
        public string UsagePolicy { get; set; } = VfxReferenceUsagePolicies.LicensedReusable;
        public string LicenseId { get; set; } = string.Empty;
        public bool PublicRedistributionAllowed { get; set; }
        public string ScannerId { get; set; } = "mx.vfx.source-library.unity";
        public string ScannerVersion { get; set; } = "1";
        public bool DeepScanComplete { get; set; }
        public int PrefabCount { get; set; }
        public int ParticleSystemCount { get; set; }
        public int SupportAssetCount { get; set; }
        public List<VfxSourcePackageEntry> Packages { get; set; } = new List<VfxSourcePackageEntry>();
        public List<VfxSourceEffectEntry> Effects { get; set; } = new List<VfxSourceEffectEntry>();
        public List<VfxSourceComponentEntry> Components { get; set; } = new List<VfxSourceComponentEntry>();
        public List<VfxSourceSupportAssetEntry> SupportAssets { get; set; } = new List<VfxSourceSupportAssetEntry>();
        public List<VfxSourceDuplicateCluster> DuplicateClusters { get; set; } = new List<VfxSourceDuplicateCluster>();
        public string IndexHash { get; set; } = string.Empty;
    }

    public sealed class VfxSourcePackageEntry
    {
        public string SourcePackageId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        public string LicenseId { get; set; } = string.Empty;
        public bool PublicRedistributionAllowed { get; set; }
        public int PrefabCount { get; set; }
        public int ParticleSystemCount { get; set; }
        public string ContentHash { get; set; } = string.Empty;
    }

    public sealed class VfxSourceEffectEntry
    {
        public string SourceEffectId { get; set; } = string.Empty;
        public string SourcePackageId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PrefabGuid { get; set; } = string.Empty;
        public string PrefabPath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public string QualityTier { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public int ParticleSystemCount { get; set; }
        public List<string> ComponentIds { get; set; } = new List<string>();
    }

    public sealed class VfxSourceComponentEntry
    {
        public string ComponentId { get; set; } = string.Empty;
        public string ComponentKind { get; set; } = VfxSourceComponentKinds.ParticleSystemLayer;
        public string SourcePackageId { get; set; } = string.Empty;
        public string SourceEffectId { get; set; } = string.Empty;
        public string PrefabGuid { get; set; } = string.Empty;
        public string PrefabPath { get; set; } = string.Empty;
        public long LocalFileId { get; set; }
        public string ObjectPath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Backend { get; set; } = "ParticleSystem";
        public List<string> Roles { get; set; } = new List<string>();
        public string Theme { get; set; } = string.Empty;
        public string Shape { get; set; } = string.Empty;
        public string Anchor { get; set; } = string.Empty;
        public string QualityTier { get; set; } = string.Empty;
        public string SimulationSpace { get; set; } = string.Empty;
        public string RenderMode { get; set; } = string.Empty;
        public float DurationSeconds { get; set; }
        public bool Loop { get; set; }
        public int MaxParticles { get; set; }
        public List<string> MaterialGuids { get; set; } = new List<string>();
        public List<string> MeshGuids { get; set; } = new List<string>();
        public List<string> MemberComponentIds { get; set; } = new List<string>();
        public string TechnicalFingerprint { get; set; } = string.Empty;
        public string CreativeFingerprint { get; set; } = string.Empty;
        public string SourceContentHash { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public string ExistingPrimitiveId { get; set; } = string.Empty;
        public bool Preferred { get; set; }
        public bool Deprecated { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public sealed class VfxSourceSupportAssetEntry
    {
        public string AssetGuid { get; set; } = string.Empty;
        public string AssetPath { get; set; } = string.Empty;
        public string AssetKind { get; set; } = string.Empty;
        public string SourcePackageId { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public List<string> ConsumerComponentIds { get; set; } = new List<string>();
    }

    public sealed class VfxSourceDuplicateCluster
    {
        public string ClusterId { get; set; } = string.Empty;
        public string ClusterKind { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public List<string> ComponentIds { get; set; } = new List<string>();
        public string PreferredComponentId { get; set; } = string.Empty;
    }

    public sealed class VfxSourceCurationDocument
    {
        public string SchemaVersion { get; set; } = VfxSourceLibrarySchemas.CurationV1;
        public string SourceLibraryId { get; set; } = "source.piloto.studio";
        public List<VfxSourceCurationEntry> Entries { get; set; } = new List<VfxSourceCurationEntry>();
        public List<VfxSourceElementCurationOverride> ElementOverrides { get; set; } = new List<VfxSourceElementCurationOverride>();
    }

    public sealed class VfxSourceElementCurationOverride
    {
        public string ElementId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class VfxSourceCurationEntry
    {
        public string ComponentId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public string Family { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public string Shape { get; set; } = string.Empty;
        public string Anchor { get; set; } = string.Empty;
        public string TimingPhase { get; set; } = string.Empty;
        public List<string> UseCases { get; set; } = new List<string>();
        public List<string> Motions { get; set; } = new List<string>();
        public string QualityTier { get; set; } = string.Empty;
        public bool Preferred { get; set; }
        public bool Deprecated { get; set; }
        public string ExistingPrimitiveId { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public sealed class VfxSourceSearchQuery
    {
        public string Text { get; set; } = string.Empty;
        public string SourcePackageId { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public string Anchor { get; set; } = string.Empty;
        public string Shape { get; set; } = string.Empty;
        public string QualityTier { get; set; } = string.Empty;
        public string Backend { get; set; } = string.Empty;
        public int MaxParticles { get; set; }
        public int Limit { get; set; } = 50;
    }

    public sealed class VfxSourceSearchResult
    {
        public string ComponentId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PrefabPath { get; set; } = string.Empty;
        public string ObjectPath { get; set; } = string.Empty;
        public int Score { get; set; }
        public string ReuseDecision { get; set; } = string.Empty;
        public List<string> Reasons { get; set; } = new List<string>();
    }

    public sealed class VfxSourceExtractionRequest
    {
        public string PrimitiveId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string OutputPrefabPath { get; set; } = string.Empty;
        public List<string> ComponentIds { get; set; } = new List<string>();
        public float LocalScale { get; set; } = 1f;
        public List<float> ThemeTint { get; set; } = new List<float> { 1f, 1f, 1f, 1f };
        public bool DeriveMaterials { get; set; }
        public string VariantOverrideHash { get; set; } = string.Empty;
    }

    public sealed class VfxSourceExtractionPlan
    {
        public string SchemaVersion { get; set; } = VfxSourceLibrarySchemas.ExtractionPlanV1;
        public string SourceLibraryId { get; set; } = string.Empty;
        public string SourceIndexHash { get; set; } = string.Empty;
        public string PrimitiveId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string OutputPrefabPath { get; set; } = string.Empty;
        public string DependencyPolicy { get; set; } = VfxSourceDependencyPolicies.DirectReference;
        public List<VfxSourceComponentEntry> Components { get; set; } = new List<VfxSourceComponentEntry>();
        public float LocalScale { get; set; } = 1f;
        public List<float> ThemeTint { get; set; } = new List<float>();
        public bool DeriveMaterials { get; set; }
        public string VariantOverrideHash { get; set; } = string.Empty;
        public string RegistryHash { get; set; } = string.Empty;
        public string Resolution { get; set; } = VfxSourceExtractionStates.NotExtracted;
        public List<string> ReusePrimitiveIds { get; set; } = new List<string>();
        public List<string> MissingComponentIds { get; set; } = new List<string>();
        public bool Blocked { get; set; }
        public List<string> Diagnostics { get; set; } = new List<string>();
        public string PlanHash { get; set; } = string.Empty;
    }

    public static class VfxSourceExtractionStates
    {
        public const string NotExtracted = "NotExtracted";
        public const string ReuseExisting = "ReuseExisting";
        public const string PartiallyExtracted = "PartiallyExtracted";
        public const string VariantRequired = "VariantRequired";
        public const string Stale = "Stale";
        public const string Conflict = "Conflict";
    }

    public sealed class VfxSourceExtractionRegistryDocument
    {
        public string SchemaVersion { get; set; } = VfxSourceLibrarySchemas.ExtractionRegistryV1;
        public string SourceLibraryId { get; set; } = "source.piloto.studio";
        public string SourceIndexHash { get; set; } = string.Empty;
        public List<VfxSourceExtractionRegistryEntry> Entries { get; set; } = new List<VfxSourceExtractionRegistryEntry>();
        public string RegistryHash { get; set; } = string.Empty;
    }

    public sealed class VfxSourceExtractionRegistryEntry
    {
        public string PrimitiveId { get; set; } = string.Empty;
        public string PrefabGuid { get; set; } = string.Empty;
        public string PrefabPath { get; set; } = string.Empty;
        public List<string> SourceComponentIds { get; set; } = new List<string>();
        public List<string> TechnicalFingerprints { get; set; } = new List<string>();
        public string MemberSetHash { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public string DerivationMode { get; set; } = "ExtractedLayer";
        public string VariantOverrideHash { get; set; } = string.Empty;
        public string ExtractorId { get; set; } = string.Empty;
        public string ExtractorVersion { get; set; } = string.Empty;
        public string CatalogEvidenceHash { get; set; } = string.Empty;
        public string ReceiptHash { get; set; } = string.Empty;
        public List<string> EquivalentComponentIds { get; set; } = new List<string>();
        public bool Stale { get; set; }
    }

    public sealed class VfxSourceExtractionReceipt
    {
        public string SchemaVersion { get; set; } = VfxSourceLibrarySchemas.ExtractionReceiptV1;
        public string PlanHash { get; set; } = string.Empty;
        public string PrimitiveId { get; set; } = string.Empty;
        public string OutputPrefabPath { get; set; } = string.Empty;
        public string OutputPrefabGuid { get; set; } = string.Empty;
        public string OutputContentHash { get; set; } = string.Empty;
        public string OutputDependencyHash { get; set; } = string.Empty;
        public List<string> SourceComponentIds { get; set; } = new List<string>();
        public List<string> DirectSourceDependencyGuids { get; set; } = new List<string>();
        public List<string> DerivedAssetPaths { get; set; } = new List<string>();
        public List<string> ForbiddenComponents { get; set; } = new List<string>();
        public bool UnitySerializationUsed { get; set; }
        public string ExtractorId { get; set; } = "mx.vfx.source-component-extractor";
        public string ExtractorVersion { get; set; } = "1";
        public string ReceiptHash { get; set; } = string.Empty;
    }

    public sealed class VfxSourceCompositionLayer
    {
        public string LayerId { get; set; } = string.Empty;
        public string PrimitiveId { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public sealed class VfxSourceCompositionPlan
    {
        public string SchemaVersion { get; set; } = VfxSourceLibrarySchemas.CompositionPlanV1;
        public string RecipeId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string OutputPrefabPath { get; set; } = string.Empty;
        public string ExpectedCatalogHash { get; set; } = string.Empty;
        public List<VfxSourceCompositionLayer> Layers { get; set; } = new List<VfxSourceCompositionLayer>();
        public bool Blocked { get; set; }
        public List<string> Diagnostics { get; set; } = new List<string>();
        public string PlanHash { get; set; } = string.Empty;
    }

    public sealed class VfxSourceValidationReport
    {
        public bool Valid { get; set; }
        public List<string> Diagnostics { get; set; } = new List<string>();
    }

    public interface IVfxSourceLibraryScanner
    {
        VfxSourceLibraryDocument Scan(string sourceRoot, VfxSourceCurationDocument curation);
    }

    public interface IVfxSourceSearchService
    {
        IReadOnlyList<VfxSourceSearchResult> Search(VfxSourceLibraryDocument index, VfxSourceSearchQuery query);
    }

    public interface IVfxSourceComponentExtractor
    {
        VfxSourceExtractionReceipt Extract(VfxSourceExtractionPlan plan);
    }

    public interface IVfxRecipeAssembler
    {
        object Assemble(VfxSourceCompositionPlan plan);
    }

    public interface IVfxSourceDependencyPolicy
    {
        bool IsDirectReferenceAllowed(string assetPath, string assetKind);
    }

    public sealed class VfxSourceSearchService : IVfxSourceSearchService
    {
        public IReadOnlyList<VfxSourceSearchResult> Search(VfxSourceLibraryDocument index, VfxSourceSearchQuery query)
        {
            if (index == null) return Array.Empty<VfxSourceSearchResult>();
            query ??= new VfxSourceSearchQuery();
            var results = new List<VfxSourceSearchResult>();
            for (int i = 0; i < index.Components.Count; i++)
            {
                VfxSourceComponentEntry component = index.Components[i];
                if (component == null || component.Deprecated || !Matches(component, index, query)) continue;
                int score = component.Preferred ? 20 : 0;
                var reasons = new List<string>();
                Score(component.Roles.Contains(query.Role), query.Role, 30, "role", ref score, reasons);
                Score(EqualsValue(component.Theme, query.Theme), query.Theme, 16, "theme", ref score, reasons);
                Score(EqualsValue(component.Anchor, query.Anchor), query.Anchor, 14, "anchor", ref score, reasons);
                Score(EqualsValue(component.Shape, query.Shape), query.Shape, 12, "shape", ref score, reasons);
                Score(EqualsValue(component.QualityTier, query.QualityTier), query.QualityTier, 8, "quality", ref score, reasons);
                if (!string.IsNullOrWhiteSpace(component.ExistingPrimitiveId)) { score += 40; reasons.Add("existing-project-primitive"); }
                results.Add(new VfxSourceSearchResult
                {
                    ComponentId = component.ComponentId,
                    DisplayName = component.DisplayName,
                    PrefabPath = component.PrefabPath,
                    ObjectPath = component.ObjectPath,
                    Score = score,
                    ReuseDecision = string.IsNullOrWhiteSpace(component.ExistingPrimitiveId) ? "ExtractSourceComponent" : "ReuseExistingPrimitive",
                    Reasons = reasons
                });
            }
            return results.OrderByDescending(value => value.Score).ThenBy(value => value.ComponentId, StringComparer.Ordinal).Take(Math.Max(1, Math.Min(500, query.Limit))).ToArray();
        }

        private static bool Matches(VfxSourceComponentEntry component, VfxSourceLibraryDocument index, VfxSourceSearchQuery query)
        {
            VfxSourceEffectEntry effect = index.Effects.FirstOrDefault(value => value.SourceEffectId == component.SourceEffectId);
            if (!BlankOrEqual(query.SourcePackageId, component.SourcePackageId) || !BlankOrEqual(query.Family, effect?.Family) ||
                !BlankOrContains(query.Role, component.Roles) || !BlankOrEqual(query.Theme, component.Theme) ||
                !BlankOrEqual(query.Anchor, component.Anchor) || !BlankOrEqual(query.Shape, component.Shape) ||
                !BlankOrEqual(query.QualityTier, component.QualityTier) || !BlankOrEqual(query.Backend, component.Backend)) return false;
            if (query.MaxParticles > 0 && component.MaxParticles > query.MaxParticles) return false;
            string text = query.Text?.Trim() ?? string.Empty;
            return text.Length == 0 || Contains(component.DisplayName, text) || Contains(component.ObjectPath, text) || component.Roles.Any(value => Contains(value, text));
        }

        private static void Score(bool matched, string requested, int value, string reason, ref int score, List<string> reasons)
        { if (!string.IsNullOrWhiteSpace(requested) && matched) { score += value; reasons.Add(reason); } }
        private static bool BlankOrEqual(string expected, string actual) => string.IsNullOrWhiteSpace(expected) || EqualsValue(expected, actual);
        private static bool BlankOrContains(string expected, List<string> values) => string.IsNullOrWhiteSpace(expected) || values.Any(value => EqualsValue(value, expected));
        private static bool EqualsValue(string left, string right) => string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        private static bool Contains(string value, string text) => (value ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static class VfxSourcePlanner
    {
        public static VfxSourceExtractionPlan PlanExtraction(VfxSourceLibraryDocument index, VfxSourceExtractionRequest request)
            => PlanExtraction(index, request, null);

        public static VfxSourceExtractionPlan PlanExtraction(VfxSourceLibraryDocument index, VfxSourceExtractionRequest request, VfxSourceExtractionRegistryDocument registry)
        {
            var plan = new VfxSourceExtractionPlan
            {
                SourceLibraryId = index?.SourceLibraryId ?? string.Empty,
                SourceIndexHash = index?.IndexHash ?? string.Empty,
                PrimitiveId = request?.PrimitiveId ?? string.Empty,
                DisplayName = request?.DisplayName ?? string.Empty,
                OutputPrefabPath = NormalizePath(request?.OutputPrefabPath),
                LocalScale = request?.LocalScale ?? 1f,
                ThemeTint = request?.ThemeTint?.ToList() ?? new List<float>(),
                DeriveMaterials = request?.DeriveMaterials == true,
                VariantOverrideHash = request?.VariantOverrideHash ?? string.Empty,
                DependencyPolicy = request?.DeriveMaterials == true ? VfxSourceDependencyPolicies.DerivedWhenModified : VfxSourceDependencyPolicies.DirectReference,
                RegistryHash = registry?.RegistryHash ?? string.Empty
            };
            if (index == null || !index.DeepScanComplete || !VfxCreativeFlywheelHash.IsSha256(index.IndexHash)) plan.Diagnostics.Add("A current deep Source Library index is required.");
            if (!IsStableId(plan.PrimitiveId)) plan.Diagnostics.Add("Primitive id must be a stable vfx.primitive.* id.");
            if (!IsProjectPrefab(plan.OutputPrefabPath) || !plan.OutputPrefabPath.StartsWith("Assets/VFX/MxFramework/", StringComparison.Ordinal)) plan.Diagnostics.Add("Output Prefab must be below Assets/VFX/MxFramework/.");
            if (request?.ComponentIds == null || request.ComponentIds.Count == 0) plan.Diagnostics.Add("At least one source component is required.");
            else if (index != null)
                foreach (string id in request.ComponentIds.Distinct(StringComparer.Ordinal))
                {
                    VfxSourceComponentEntry component = index.Components.FirstOrDefault(value => value.ComponentId == id);
                    if (component == null) plan.Diagnostics.Add("Unknown source component: " + id);
                    else if (component.Deprecated) plan.Diagnostics.Add("Deprecated source component cannot be extracted: " + id);
                    else plan.Components.Add(component);
                }
            ResolveExistingExtraction(index, request, registry, plan);
            if (plan.LocalScale <= 0f || plan.LocalScale > 16f) plan.Diagnostics.Add("Local scale must be in (0,16].");
            if (plan.ThemeTint.Count != 4 || plan.ThemeTint.Any(value => value < 0f || value > 4f)) plan.Diagnostics.Add("Theme tint must contain four values in [0,4].");
            if (plan.DeriveMaterials && !VfxCreativeFlywheelHash.IsSha256(request?.VariantOverrideHash)) plan.Diagnostics.Add("A material/render-state Variant requires a stable variantOverrideHash.");
            plan.Blocked = plan.Diagnostics.Count > 0;
            plan.PlanHash = VfxCreativeFlywheelHash.Compute(plan, "planHash");
            return plan;
        }

        private static void ResolveExistingExtraction(VfxSourceLibraryDocument index, VfxSourceExtractionRequest request, VfxSourceExtractionRegistryDocument registry, VfxSourceExtractionPlan plan)
        {
            if (index == null || plan.Components.Count == 0) return;
            List<VfxSourceExtractionRegistryEntry> entries = registry?.Entries ?? new List<VfxSourceExtractionRegistryEntry>();
            var reuse = new HashSet<string>(StringComparer.Ordinal);
            var missing = new List<VfxSourceComponentEntry>();
            foreach (VfxSourceComponentEntry component in plan.Components)
            {
                VfxSourceExtractionRegistryEntry match = entries.FirstOrDefault(value => !value.Stale &&
                    ((value.SourceComponentIds ?? new List<string>()).Contains(component.ComponentId, StringComparer.Ordinal)
                     || (value.TechnicalFingerprints ?? new List<string>()).Contains(component.TechnicalFingerprint, StringComparer.Ordinal)));
                string primitiveId = match?.PrimitiveId;
                if (string.IsNullOrWhiteSpace(primitiveId)) primitiveId = component.ExistingPrimitiveId;
                if (string.IsNullOrWhiteSpace(primitiveId)) missing.Add(component); else reuse.Add(primitiveId);
            }
            plan.ReusePrimitiveIds = reuse.OrderBy(value => value, StringComparer.Ordinal).ToList();
            plan.MissingComponentIds = missing.Select(value => value.ComponentId).OrderBy(value => value, StringComparer.Ordinal).ToList();
            if (plan.DeriveMaterials && reuse.Count > 0)
            {
                plan.Resolution = VfxSourceExtractionStates.VariantRequired;
                return;
            }
            if (missing.Count == 0 && reuse.Count > 0)
            {
                plan.Resolution = VfxSourceExtractionStates.ReuseExisting;
                plan.Components.Clear();
            }
            else if (reuse.Count > 0)
            {
                plan.Resolution = VfxSourceExtractionStates.PartiallyExtracted;
                plan.Components = missing;
            }
            if (registry == null) return;
            VfxSourceExtractionRegistryEntry idConflict = entries.FirstOrDefault(value => value.PrimitiveId == plan.PrimitiveId && !plan.ReusePrimitiveIds.Contains(value.PrimitiveId));
            VfxSourceExtractionRegistryEntry pathConflict = entries.FirstOrDefault(value => string.Equals(value.PrefabPath, plan.OutputPrefabPath, StringComparison.OrdinalIgnoreCase) && !plan.ReusePrimitiveIds.Contains(value.PrimitiveId));
            if (idConflict != null || pathConflict != null)
            {
                plan.Resolution = VfxSourceExtractionStates.Conflict;
                plan.Diagnostics.Add("Primitive id or output path is already registered for a different source extraction.");
            }
        }

        public static VfxSourceCompositionPlan PlanComposition(string recipeId, string displayName, string outputPrefabPath, string catalogHash, IEnumerable<string> primitiveIds)
        {
            var plan = new VfxSourceCompositionPlan { RecipeId = recipeId ?? string.Empty, DisplayName = displayName ?? string.Empty, OutputPrefabPath = NormalizePath(outputPrefabPath), ExpectedCatalogHash = catalogHash ?? string.Empty };
            int order = 0;
            foreach (string id in primitiveIds ?? Array.Empty<string>()) plan.Layers.Add(new VfxSourceCompositionLayer { LayerId = "layer." + order, PrimitiveId = id ?? string.Empty, Order = order++ });
            if (!IsStableId(plan.RecipeId) || !plan.RecipeId.StartsWith("vfx.recipe.", StringComparison.Ordinal)) plan.Diagnostics.Add("Recipe id must be a stable vfx.recipe.* id.");
            if (!IsProjectPrefab(plan.OutputPrefabPath)) plan.Diagnostics.Add("Composition output must be a project Prefab path.");
            if (!VfxCreativeFlywheelHash.IsSha256(plan.ExpectedCatalogHash) && !IsRawSha256(plan.ExpectedCatalogHash)) plan.Diagnostics.Add("Expected Catalog hash is required.");
            if (plan.Layers.Count == 0 || plan.Layers.Any(value => !IsStableId(value.PrimitiveId))) plan.Diagnostics.Add("Composition requires stable Primitive ids.");
            plan.Blocked = plan.Diagnostics.Count > 0;
            plan.PlanHash = VfxCreativeFlywheelHash.Compute(plan, "planHash");
            return plan;
        }

        private static bool IsStableId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '.' || c == '_' || c == '-');
        private static bool IsRawSha256(string value) => !string.IsNullOrWhiteSpace(value) && value.Length == 64 && value.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        private static bool IsProjectPrefab(string value) => !string.IsNullOrWhiteSpace(value) && value.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !Path.IsPathRooted(value) && !value.Contains("..");
        private static string NormalizePath(string value) => (value ?? string.Empty).Replace('\\', '/').Trim();
    }

    public static class VfxSourceLibraryValidator
    {
        public static VfxSourceExtractionRegistryDocument SealRegistry(VfxSourceExtractionRegistryDocument registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.Entries ??= new List<VfxSourceExtractionRegistryEntry>();
            foreach (VfxSourceExtractionRegistryEntry entry in registry.Entries)
            {
                entry.SourceComponentIds = (entry.SourceComponentIds ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                entry.TechnicalFingerprints = (entry.TechnicalFingerprints ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                entry.EquivalentComponentIds = (entry.EquivalentComponentIds ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                entry.MemberSetHash = VfxCreativeFlywheelHash.Compute(entry.SourceComponentIds);
            }
            registry.Entries = registry.Entries.OrderBy(value => value.PrimitiveId, StringComparer.Ordinal).ToList();
            registry.RegistryHash = string.Empty;
            registry.RegistryHash = VfxCreativeFlywheelHash.Compute(registry, "registryHash");
            return registry;
        }

        public static VfxSourceValidationReport ValidateRegistry(VfxSourceExtractionRegistryDocument registry, VfxSourceLibraryDocument index)
        {
            var report = new VfxSourceValidationReport { Valid = true };
            if (registry == null || registry.SchemaVersion != VfxSourceLibrarySchemas.ExtractionRegistryV1) report.Diagnostics.Add("Extraction registry schema is invalid.");
            if (index == null || !string.Equals(registry?.SourceLibraryId, index.SourceLibraryId, StringComparison.Ordinal)) report.Diagnostics.Add("Extraction registry source library does not match the index.");
            var primitiveIds = new HashSet<string>(StringComparer.Ordinal);
            var prefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var exactComponents = new Dictionary<string, string>(StringComparer.Ordinal);
            var fingerprintOwners = new Dictionary<string, VfxSourceExtractionRegistryEntry>(StringComparer.Ordinal);
            foreach (VfxSourceExtractionRegistryEntry entry in registry?.Entries ?? new List<VfxSourceExtractionRegistryEntry>())
            {
                if (!primitiveIds.Add(entry.PrimitiveId)) report.Diagnostics.Add("Duplicate registry Primitive id: " + entry.PrimitiveId);
                if (!prefabPaths.Add(entry.PrefabPath)) report.Diagnostics.Add("Duplicate registry Prefab path: " + entry.PrefabPath);
                foreach (string componentId in entry.SourceComponentIds ?? new List<string>())
                {
                    if (index != null && !index.Components.Any(value => value.ComponentId == componentId)) report.Diagnostics.Add("Registry references an unknown source component: " + componentId);
                    if (exactComponents.TryGetValue(componentId, out string owner) && owner != entry.PrimitiveId) report.Diagnostics.Add("Source component is registered to multiple Primitives: " + componentId);
                    else exactComponents[componentId] = entry.PrimitiveId;
                }
                foreach (string fingerprint in entry.TechnicalFingerprints ?? new List<string>())
                {
                    if (fingerprintOwners.TryGetValue(fingerprint, out VfxSourceExtractionRegistryEntry owner) && owner.PrimitiveId != entry.PrimitiveId
                        && string.IsNullOrWhiteSpace(owner.VariantOverrideHash) && string.IsNullOrWhiteSpace(entry.VariantOverrideHash))
                        report.Diagnostics.Add("Technical fingerprint is registered to multiple canonical Primitives: " + fingerprint);
                    else if (owner == null || string.IsNullOrWhiteSpace(entry.VariantOverrideHash)) fingerprintOwners[fingerprint] = entry;
                }
                string expectedMemberHash = VfxCreativeFlywheelHash.Compute((entry.SourceComponentIds ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
                if (!string.Equals(entry.MemberSetHash, expectedMemberHash, StringComparison.Ordinal)) report.Diagnostics.Add("Registry memberSetHash is stale: " + entry.PrimitiveId);
            }
            if (registry != null && !string.Equals(registry.RegistryHash, VfxCreativeFlywheelHash.Compute(registry, "registryHash"), StringComparison.Ordinal)) report.Diagnostics.Add("Extraction registry hash is invalid.");
            report.Valid = report.Diagnostics.Count == 0;
            return report;
        }

        public static VfxSourceLibraryDocument SealIndex(VfxSourceLibraryDocument index)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            index.Packages = (index.Packages ?? new List<VfxSourcePackageEntry>()).OrderBy(value => value.SourcePackageId, StringComparer.Ordinal).ToList();
            index.Effects = (index.Effects ?? new List<VfxSourceEffectEntry>()).OrderBy(value => value.SourceEffectId, StringComparer.Ordinal).ToList();
            index.Components = (index.Components ?? new List<VfxSourceComponentEntry>()).OrderBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
            index.SupportAssets = (index.SupportAssets ?? new List<VfxSourceSupportAssetEntry>()).OrderBy(value => value.AssetGuid, StringComparer.Ordinal).ToList();
            index.DuplicateClusters = (index.DuplicateClusters ?? new List<VfxSourceDuplicateCluster>()).OrderBy(value => value.ClusterId, StringComparer.Ordinal).ToList();
            foreach (VfxSourceEffectEntry effect in index.Effects)
            {
                effect.Roles = (effect.Roles ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                effect.ComponentIds = (effect.ComponentIds ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            }
            foreach (VfxSourceComponentEntry component in index.Components)
            {
                component.Roles = (component.Roles ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                component.MaterialGuids = (component.MaterialGuids ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                component.MeshGuids = (component.MeshGuids ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                component.MemberComponentIds = (component.MemberComponentIds ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            }
            foreach (VfxSourceSupportAssetEntry asset in index.SupportAssets)
                asset.ConsumerComponentIds = (asset.ConsumerComponentIds ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            foreach (VfxSourceDuplicateCluster cluster in index.DuplicateClusters)
                cluster.ComponentIds = (cluster.ComponentIds ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            index.PrefabCount = index.Effects.Count;
            index.ParticleSystemCount = index.Components.Count(value => value.ComponentKind == VfxSourceComponentKinds.ParticleSystemLayer);
            index.SupportAssetCount = index.SupportAssets.Count;
            index.IndexHash = VfxCreativeFlywheelHash.Compute(index, "indexHash");
            return index;
        }

        public static VfxSourceExtractionReceipt SealReceipt(VfxSourceExtractionReceipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            receipt.SourceComponentIds = (receipt.SourceComponentIds ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            receipt.DirectSourceDependencyGuids = (receipt.DirectSourceDependencyGuids ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            receipt.DerivedAssetPaths = (receipt.DerivedAssetPaths ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            receipt.ForbiddenComponents = (receipt.ForbiddenComponents ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            receipt.ReceiptHash = VfxCreativeFlywheelHash.Compute(receipt, "receiptHash");
            return receipt;
        }

        public static VfxSourceValidationReport ValidateIndex(VfxSourceLibraryDocument index)
        {
            var report = new VfxSourceValidationReport();
            if (index == null || index.SchemaVersion != VfxSourceLibrarySchemas.IndexV1) report.Diagnostics.Add("Source Library schema is invalid.");
            else
            {
                if (index.UsagePolicy != VfxReferenceUsagePolicies.LicensedReusable || string.IsNullOrWhiteSpace(index.LicenseId) || !index.PublicRedistributionAllowed) report.Diagnostics.Add("Reusable source authorization is incomplete.");
                if (!index.DeepScanComplete) report.Diagnostics.Add("Unity deep scan has not completed.");
                if (index.PrefabCount != index.Effects.Count) report.Diagnostics.Add("Prefab/effect counts do not match.");
                if (index.ParticleSystemCount != index.Components.Count(value => value.ComponentKind == VfxSourceComponentKinds.ParticleSystemLayer)) report.Diagnostics.Add("ParticleSystem/component counts do not match.");
                if (index.Components.GroupBy(value => value.ComponentId).Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() != 1)) report.Diagnostics.Add("Source component ids must be unique and non-empty.");
                if (index.Effects.GroupBy(value => value.SourceEffectId).Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() != 1)) report.Diagnostics.Add("Source effect ids must be unique and non-empty.");
                if (index.Components.Any(value => !VfxCreativeFlywheelHash.IsUnityGuid(value.PrefabGuid) || value.LocalFileId == 0 || !VfxCreativeFlywheelHash.IsSha256(value.TechnicalFingerprint) || !VfxCreativeFlywheelHash.IsUnityDependencyHash(value.DependencyHash))) report.Diagnostics.Add("Source component technical evidence is incomplete.");
                if (index.SupportAssets.Any(value => !VfxCreativeFlywheelHash.IsUnityGuid(value.AssetGuid) || !VfxCreativeFlywheelHash.IsSha256(value.ContentHash))) report.Diagnostics.Add("Support asset evidence is incomplete.");
                string observed = VfxCreativeFlywheelHash.Compute(index, "indexHash");
                if (!string.Equals(observed, index.IndexHash, StringComparison.OrdinalIgnoreCase)) report.Diagnostics.Add("Source Library index hash is stale or invalid.");
            }
            report.Valid = report.Diagnostics.Count == 0;
            return report;
        }

        public static VfxSourceValidationReport ValidateExtraction(VfxSourceExtractionPlan plan, VfxSourceExtractionReceipt receipt, VfxSourceLibraryDocument index)
        {
            var report = ValidateIndex(index);
            if (plan == null || plan.Blocked || !string.Equals(plan.PlanHash, VfxCreativeFlywheelHash.Compute(plan, "planHash"), StringComparison.OrdinalIgnoreCase)) report.Diagnostics.Add("Extraction plan is missing, blocked, or tampered.");
            if (receipt == null || receipt.SchemaVersion != VfxSourceLibrarySchemas.ExtractionReceiptV1) report.Diagnostics.Add("Extraction receipt is missing or incompatible.");
            else if (plan != null)
            {
                if (!string.Equals(receipt.PlanHash, plan.PlanHash, StringComparison.OrdinalIgnoreCase) || receipt.PrimitiveId != plan.PrimitiveId || receipt.OutputPrefabPath != plan.OutputPrefabPath) report.Diagnostics.Add("Extraction receipt does not match its plan.");
                if (!receipt.UnitySerializationUsed) report.Diagnostics.Add("Extraction must be serialized by Unity Editor API.");
                if (receipt.ForbiddenComponents.Count > 0) report.Diagnostics.Add("Extracted Prefab contains forbidden components.");
                if (!VfxCreativeFlywheelHash.IsUnityGuid(receipt.OutputPrefabGuid) || !VfxCreativeFlywheelHash.IsSha256(receipt.OutputContentHash) || !VfxCreativeFlywheelHash.IsUnityDependencyHash(receipt.OutputDependencyHash)) report.Diagnostics.Add("Output GUID/content/dependency evidence is incomplete.");
                if (!plan.Components.Select(value => value.ComponentId).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(receipt.SourceComponentIds.OrderBy(value => value, StringComparer.Ordinal))) report.Diagnostics.Add("Receipt source component membership differs from the plan.");
                if (!string.Equals(receipt.ReceiptHash, VfxCreativeFlywheelHash.Compute(receipt, "receiptHash"), StringComparison.OrdinalIgnoreCase)) report.Diagnostics.Add("Extraction receipt hash is invalid.");
            }
            report.Valid = report.Diagnostics.Count == 0;
            return report;
        }
    }
}
