using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.Resources;

namespace MxFramework.Editor.Animation
{
    internal enum MxAnimationModOverridePreviewRowStatus
    {
        Accepted = 0,
        Rejected = 1,
        Diagnostic = 2
    }

    internal sealed class MxAnimationModOverridePreviewRow
    {
        public MxAnimationModOverridePreviewRow(
            MxAnimationModOverridePreviewRowStatus status,
            string category,
            string target,
            string code,
            string field,
            string message)
        {
            Status = status;
            Category = category ?? string.Empty;
            Target = target ?? string.Empty;
            Code = code ?? string.Empty;
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MxAnimationModOverridePreviewRowStatus Status { get; }
        public string Category { get; }
        public string Target { get; }
        public string Code { get; }
        public string Field { get; }
        public string Message { get; }
    }

    internal sealed class MxAnimationModOverrideWorkstationPreview
    {
        private readonly List<MxAnimationModOverridePreviewRow> _rows;

        public MxAnimationModOverrideWorkstationPreview(
            MxAnimationSetDefinition baseDefinition,
            MxAnimationModOverrideDefinition overrideDefinition,
            MxAnimationModOverrideMergeResult mergeResult,
            MxAnimationPackageValidationReport packageValidation,
            MxAnimationWarmupResult warmupResult,
            IReadOnlyList<MxAnimationModOverridePreviewRow> rows,
            string reportText)
        {
            BaseDefinition = baseDefinition;
            OverrideDefinition = overrideDefinition;
            MergeResult = mergeResult;
            PackageValidation = packageValidation ?? new MxAnimationPackageValidationReport();
            WarmupResult = warmupResult;
            _rows = rows != null
                ? new List<MxAnimationModOverridePreviewRow>(rows)
                : new List<MxAnimationModOverridePreviewRow>();
            ReportText = reportText ?? string.Empty;
        }

        public MxAnimationSetDefinition BaseDefinition { get; }
        public MxAnimationModOverrideDefinition OverrideDefinition { get; }
        public MxAnimationModOverrideMergeResult MergeResult { get; }
        public MxAnimationPackageValidationReport PackageValidation { get; }
        public MxAnimationWarmupResult WarmupResult { get; }
        public IReadOnlyList<MxAnimationModOverridePreviewRow> Rows => _rows;
        public string ReportText { get; }
        public bool Success => MergeResult != null
            && MergeResult.Success
            && PackageValidation.Success
            && (WarmupResult == null || WarmupResult.Success);
    }

    internal static class MxAnimationModOverrideWorkstationPreviewBuilder
    {
        public static MxAnimationModOverrideWorkstationPreview BuildFromRegistries(
            MxAnimationClipRegistryAsset baseRegistry,
            MxAnimationClipRegistryAsset overrideRegistry,
            MxAnimationPackageBuildResult packageBuildResult,
            MxAnimationCompatibilityWorkstationReport compatibilityReport,
            int overrideVersion,
            int resultVersion,
            int loadOrder,
            bool runWarmupValidation)
        {
            MxAnimationClipRegistryExportResult baseExport =
                MxAnimationClipRegistryExporter.ExportStructureOnly(baseRegistry);
            MxAnimationClipRegistryExportResult overrideExport =
                MxAnimationClipRegistryExporter.ExportStructureOnly(overrideRegistry);
            MxAnimationSetDefinition baseDefinition = baseExport != null ? baseExport.Definition : null;
            MxAnimationSetDefinition overrideDefinition = overrideExport != null ? overrideExport.Definition : null;
            MxAnimationPackageExpectation packageExpectation =
                packageBuildResult != null ? packageBuildResult.Expectation : null;

            MxAnimationModOverrideDefinition modOverride = CreateOverrideDefinition(
                baseDefinition,
                overrideDefinition,
                packageExpectation,
                compatibilityReport,
                overrideVersion,
                resultVersion,
                loadOrder);

            ResourceCatalog catalog = packageBuildResult != null ? packageBuildResult.CatalogSnapshot : null;
            MxAnimationPackageCatalog packageCatalog = packageBuildResult != null ? packageBuildResult.PackageCatalog : null;
            return Build(
                baseDefinition,
                modOverride,
                catalog,
                packageCatalog,
                packageExpectation,
                compatibilityReport != null ? compatibilityReport.Profile : null,
                baseExport,
                overrideExport,
                packageBuildResult,
                compatibilityReport,
                runWarmupValidation);
        }

        public static MxAnimationModOverrideWorkstationPreview Build(
            MxAnimationSetDefinition baseDefinition,
            MxAnimationModOverrideDefinition overrideDefinition,
            ResourceCatalog catalog,
            MxAnimationPackageCatalog packageCatalog,
            MxAnimationPackageExpectation basePackageExpectation,
            MxAnimationCompatibilityProfile compatibilityProfile,
            MxAnimationClipRegistryExportResult baseExport = null,
            MxAnimationClipRegistryExportResult overrideExport = null,
            MxAnimationPackageBuildResult packageBuildResult = null,
            MxAnimationCompatibilityWorkstationReport compatibilityReport = null,
            bool runWarmupValidation = false)
        {
            MxAnimationModOverrideMergeResult merge = MxAnimationModOverrideMerger.Merge(
                new MxAnimationModOverrideMergeRequest(
                    baseDefinition,
                    overrideDefinition,
                    catalog,
                    compatibilityProfile,
                    packageCatalog,
                    basePackageExpectation));

            MxAnimationPackageValidationReport packageValidation = ValidatePackage(
                merge != null ? merge.MergedPackageExpectation : null,
                packageCatalog,
                catalog);
            MxAnimationWarmupResult warmup = runWarmupValidation
                ? RunWarmupValidation(merge, catalog, packageCatalog, compatibilityProfile)
                : null;
            List<MxAnimationModOverridePreviewRow> rows = CreateRows(baseDefinition, overrideDefinition, merge);
            string reportText = CreateReportText(
                baseDefinition,
                overrideDefinition,
                merge,
                packageValidation,
                warmup,
                rows,
                baseExport,
                overrideExport,
                packageBuildResult,
                compatibilityReport);

            return new MxAnimationModOverrideWorkstationPreview(
                baseDefinition,
                overrideDefinition,
                merge,
                packageValidation,
                warmup,
                rows,
                reportText);
        }

        private static MxAnimationModOverrideDefinition CreateOverrideDefinition(
            MxAnimationSetDefinition baseDefinition,
            MxAnimationSetDefinition overrideDefinition,
            MxAnimationPackageExpectation packageExpectation,
            MxAnimationCompatibilityWorkstationReport compatibilityReport,
            int overrideVersion,
            int resultVersion,
            int loadOrder)
        {
            string packageId = packageExpectation != null && !string.IsNullOrWhiteSpace(packageExpectation.PackageId)
                ? packageExpectation.PackageId
                : overrideDefinition != null && !string.IsNullOrWhiteSpace(overrideDefinition.SetId)
                    ? overrideDefinition.SetId + ".mod"
                    : string.Empty;
            int packageVersion = packageExpectation != null && packageExpectation.Version > 0
                ? packageExpectation.Version
                : Math.Max(1, overrideVersion);
            string catalogId = packageExpectation != null ? packageExpectation.CatalogId : string.Empty;
            string catalogHash = packageExpectation != null ? packageExpectation.CatalogHash : string.Empty;
            var manifest = new MxAnimationModPackageManifest(
                packageId,
                packageVersion,
                packageId,
                catalogId,
                catalogHash,
                loadOrder);

            return new MxAnimationModOverrideDefinition(
                baseDefinition != null ? baseDefinition.SetId : string.Empty,
                manifest,
                Math.Max(1, overrideVersion),
                expectedBaseVersion: baseDefinition != null ? baseDefinition.Version : 0,
                expectedBaseHash: baseDefinition != null ? baseDefinition.DefinitionHash : string.Empty,
                resultVersion: resultVersion,
                actionOverrides: CreateActionOverrides(overrideDefinition),
                layerOverrides: CreateLayerOverrides(overrideDefinition),
                blend1DOverrides: CreateBlend1DOverrides(overrideDefinition),
                blend2DOverrides: CreateBlend2DOverrides(overrideDefinition),
                packageResources: packageExpectation != null ? packageExpectation.Resources : null,
                compatibilityExpectation: compatibilityReport != null ? compatibilityReport.Expectation : null,
                acceptedProviderIds: packageExpectation != null ? packageExpectation.AcceptedProviderIds : null);
        }

        private static IReadOnlyList<MxAnimationActionBindingOverride> CreateActionOverrides(MxAnimationSetDefinition definition)
        {
            var overrides = new List<MxAnimationActionBindingOverride>();
            if (definition == null)
                return overrides;

            for (int i = 0; i < definition.Actions.Count; i++)
            {
                MxAnimationActionBinding action = definition.Actions[i];
                if (action != null)
                    overrides.Add(new MxAnimationActionBindingOverride(action, action.BindingId));
            }

            return overrides;
        }

        private static IReadOnlyList<MxAnimationLayerDefinitionOverride> CreateLayerOverrides(MxAnimationSetDefinition definition)
        {
            var overrides = new List<MxAnimationLayerDefinitionOverride>();
            if (definition == null)
                return overrides;

            for (int i = 0; i < definition.Layers.Count; i++)
            {
                MxAnimationLayerDefinition layer = definition.Layers[i];
                if (layer != null)
                    overrides.Add(new MxAnimationLayerDefinitionOverride(layer, layer.LayerId.ToString()));
            }

            return overrides;
        }

        private static IReadOnlyList<MxAnimationBlend1DDefinitionOverride> CreateBlend1DOverrides(MxAnimationSetDefinition definition)
        {
            var overrides = new List<MxAnimationBlend1DDefinitionOverride>();
            if (definition == null)
                return overrides;

            for (int i = 0; i < definition.Blend1DDefinitions.Count; i++)
            {
                MxAnimationBlend1DDefinition blend = definition.Blend1DDefinitions[i];
                if (blend != null)
                    overrides.Add(new MxAnimationBlend1DDefinitionOverride(blend, blend.BlendId));
            }

            return overrides;
        }

        private static IReadOnlyList<MxAnimationBlend2DDefinitionOverride> CreateBlend2DOverrides(MxAnimationSetDefinition definition)
        {
            var overrides = new List<MxAnimationBlend2DDefinitionOverride>();
            if (definition == null)
                return overrides;

            for (int i = 0; i < definition.Blend2DDefinitions.Count; i++)
            {
                MxAnimationBlend2DDefinition blend = definition.Blend2DDefinitions[i];
                if (blend != null)
                    overrides.Add(new MxAnimationBlend2DDefinitionOverride(blend, blend.BlendId));
            }

            return overrides;
        }

        private static MxAnimationPackageValidationReport ValidatePackage(
            MxAnimationPackageExpectation expectation,
            MxAnimationPackageCatalog packageCatalog,
            ResourceCatalog catalog)
        {
            if (expectation == null || expectation.IsDefault)
                return new MxAnimationPackageValidationReport();

            MxAnimationPackageCatalog resolved = packageCatalog ?? new MxAnimationPackageCatalog(catalog);
            return MxAnimationPackageCatalogValidator.Validate(resolved, expectation);
        }

        private static MxAnimationWarmupResult RunWarmupValidation(
            MxAnimationModOverrideMergeResult merge,
            ResourceCatalog catalog,
            MxAnimationPackageCatalog packageCatalog,
            MxAnimationCompatibilityProfile compatibilityProfile)
        {
            if (merge == null || merge.MergedDefinition == null)
                return null;

            var manager = new ResourceManager();
            var memoryProvider = new MemoryResourceProvider();
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    ResourceCatalogEntry entry = catalog.Entries[i];
                    if (entry != null && string.Equals(entry.ProviderId, "memory", StringComparison.Ordinal))
                        memoryProvider.Register(entry.Address, entry.Id);
                }

                manager.RegisterProvider(memoryProvider);
                manager.AddCatalog(catalog);
            }

            var service = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationClipRegistry clipRegistry = MxAnimationClipRegistryBuilder.FromCatalog(
                catalog,
                version: merge.MergedPackageExpectation != null ? merge.MergedPackageExpectation.Version : merge.BaseVersion,
                catalogHash: packageCatalog != null ? packageCatalog.CatalogHash : string.Empty);
            return service.Warmup(new MxAnimationWarmupRequest(
                merge.MergedDefinition,
                clipRegistry,
                catalog,
                null,
                null,
                skipPreloadWhenInvalid: false,
                compatibilityProfile,
                merge.MergedPackageExpectation,
                packageCatalog));
        }

        private static List<MxAnimationModOverridePreviewRow> CreateRows(
            MxAnimationSetDefinition baseDefinition,
            MxAnimationModOverrideDefinition overrideDefinition,
            MxAnimationModOverrideMergeResult merge)
        {
            var rows = new List<MxAnimationModOverridePreviewRow>();
            AppendInputRows(rows, baseDefinition, overrideDefinition);
            if (merge == null)
                return rows;

            for (int i = 0; i < merge.Issues.Count; i++)
            {
                MxAnimationModOverrideIssue issue = merge.Issues[i];
                rows.Add(new MxAnimationModOverridePreviewRow(
                    issue.Severity == MxAnimationModOverrideIssueSeverity.Error
                        ? MxAnimationModOverridePreviewRowStatus.Rejected
                        : MxAnimationModOverridePreviewRowStatus.Diagnostic,
                    ResolveIssueCategory(issue),
                    issue.Key.IsValid ? issue.Key.ToString() : issue.Actual,
                    issue.Code,
                    issue.Field,
                    issue.Message));
            }

            return rows;
        }

        private static void AppendInputRows(
            List<MxAnimationModOverridePreviewRow> rows,
            MxAnimationSetDefinition baseDefinition,
            MxAnimationModOverrideDefinition overrideDefinition)
        {
            if (overrideDefinition == null)
                return;

            for (int i = 0; i < overrideDefinition.ActionOverrides.Count; i++)
            {
                MxAnimationActionBindingOverride item = overrideDefinition.ActionOverrides[i];
                bool accepted = item != null && item.Binding != null && HasAction(baseDefinition, item.BindingId, item.ActionKey);
                rows.Add(InputRow(accepted, "action", item != null ? item.BindingId + "|" + item.ActionKey : string.Empty));
                if (item != null && item.Binding != null)
                    AppendEventRows(rows, accepted, item.Binding.BindingId, item.Binding.PresentationEvents);
            }

            for (int i = 0; i < overrideDefinition.LayerOverrides.Count; i++)
            {
                MxAnimationLayerDefinitionOverride item = overrideDefinition.LayerOverrides[i];
                rows.Add(InputRow(item != null && item.Layer != null, "layer", item != null ? item.LayerId.ToString() : string.Empty));
            }

            for (int i = 0; i < overrideDefinition.Blend1DOverrides.Count; i++)
            {
                MxAnimationBlend1DDefinitionOverride item = overrideDefinition.Blend1DOverrides[i];
                rows.Add(InputRow(item != null && item.Blend != null, "blend1D", item != null ? item.BlendId : string.Empty));
            }

            for (int i = 0; i < overrideDefinition.Blend2DOverrides.Count; i++)
            {
                MxAnimationBlend2DDefinitionOverride item = overrideDefinition.Blend2DOverrides[i];
                rows.Add(InputRow(item != null && item.Blend != null, "blend2D", item != null ? item.BlendId : string.Empty));
            }

            for (int i = 0; i < overrideDefinition.PackageResources.Count; i++)
            {
                MxAnimationPackageResourceExpectation item = overrideDefinition.PackageResources[i];
                rows.Add(InputRow(item != null && item.Key.IsValid, "packageResource", item != null ? item.Key.ToString() : string.Empty));
            }

            if (overrideDefinition.CompatibilityExpectation != null && !overrideDefinition.CompatibilityExpectation.IsDefault)
            {
                rows.Add(InputRow(
                    true,
                    "compatibility",
                    overrideDefinition.CompatibilityExpectation.SkeletonProfileId + "|"
                    + overrideDefinition.CompatibilityExpectation.SkeletonProfileHash));
            }
        }

        private static void AppendEventRows(
            List<MxAnimationModOverridePreviewRow> rows,
            bool accepted,
            string bindingId,
            IReadOnlyList<MxAnimationPresentationEvent> events)
        {
            if (events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                MxAnimationPresentationEvent item = events[i];
                rows.Add(InputRow(
                    accepted,
                    "event",
                    bindingId + "|" + item.EventId + "|" + item.TimeDomain + ":" + item.Time.ToString("R", CultureInfo.InvariantCulture)));
            }
        }

        private static MxAnimationModOverridePreviewRow InputRow(bool accepted, string category, string target)
        {
            return new MxAnimationModOverridePreviewRow(
                accepted ? MxAnimationModOverridePreviewRowStatus.Accepted : MxAnimationModOverridePreviewRowStatus.Rejected,
                category,
                target,
                accepted ? MxAnimationModOverrideIssueCodes.OverrideAccepted : "OverrideInputRejected",
                category,
                accepted ? "Override input is eligible for merger review." : "Override input is missing or does not target the base mapping.");
        }

        private static bool HasAction(MxAnimationSetDefinition definition, string bindingId, string actionKey)
        {
            if (definition == null)
                return false;

            for (int i = 0; i < definition.Actions.Count; i++)
            {
                MxAnimationActionBinding binding = definition.Actions[i];
                if (binding == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(bindingId) && string.Equals(binding.BindingId, bindingId, StringComparison.Ordinal))
                    return true;
                if (!string.IsNullOrWhiteSpace(actionKey) && string.Equals(binding.ActionKey, actionKey, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string ResolveIssueCategory(MxAnimationModOverrideIssue issue)
        {
            if (issue == null)
                return "diagnostic";
            if (issue.Code.StartsWith("Action", StringComparison.Ordinal))
                return "action";
            if (issue.Code.StartsWith("Layer", StringComparison.Ordinal))
                return "layer";
            if (issue.Code.StartsWith("Blend1D", StringComparison.Ordinal))
                return "blend1D";
            if (issue.Code.StartsWith("Blend2D", StringComparison.Ordinal))
                return "blend2D";
            if (string.Equals(issue.Code, MxAnimationModOverrideIssueCodes.PackageValidationFailed, StringComparison.Ordinal))
                return "packageResource";
            if (string.Equals(issue.Code, MxAnimationModOverrideIssueCodes.CompatibilityValidationFailed, StringComparison.Ordinal))
                return "compatibility";
            return "diagnostic";
        }

        private static string CreateReportText(
            MxAnimationSetDefinition baseDefinition,
            MxAnimationModOverrideDefinition overrideDefinition,
            MxAnimationModOverrideMergeResult merge,
            MxAnimationPackageValidationReport packageValidation,
            MxAnimationWarmupResult warmup,
            IReadOnlyList<MxAnimationModOverridePreviewRow> rows,
            MxAnimationClipRegistryExportResult baseExport,
            MxAnimationClipRegistryExportResult overrideExport,
            MxAnimationPackageBuildResult packageBuildResult,
            MxAnimationCompatibilityWorkstationReport compatibilityReport)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Mod Override Review Report\n");
            builder.Append("success: ").Append(merge != null && merge.Success && packageValidation.Success && (warmup == null || warmup.Success) ? "true" : "false").Append('\n');
            builder.Append("baseSetId: ").Append(baseDefinition != null ? baseDefinition.SetId : string.Empty).Append('\n');
            builder.Append("baseVersion: ").Append(merge != null ? merge.BaseVersion : baseDefinition != null ? baseDefinition.Version : 0).Append('\n');
            builder.Append("baseHash: ").Append(merge != null ? merge.BaseDefinitionHash : baseDefinition != null ? baseDefinition.DefinitionHash : string.Empty).Append('\n');
            builder.Append("expectedBaseVersion: ").Append(overrideDefinition != null ? overrideDefinition.ExpectedBaseVersion : 0).Append('\n');
            builder.Append("expectedBaseHash: ").Append(overrideDefinition != null ? overrideDefinition.ExpectedBaseHash : string.Empty).Append('\n');
            builder.Append("overrideVersion: ").Append(merge != null ? merge.OverrideVersion : overrideDefinition != null ? overrideDefinition.OverrideVersion : 0).Append('\n');
            builder.Append("overrideHash: ").Append(merge != null ? merge.OverrideHash : overrideDefinition != null ? overrideDefinition.OverrideHash : string.Empty).Append('\n');
            builder.Append("mergedVersion: ").Append(merge != null && merge.MergedDefinition != null ? merge.MergedDefinition.Version : 0).Append('\n');
            builder.Append("mergedDefinitionHash: ").Append(merge != null && merge.MergedDefinition != null ? merge.MergedDefinition.DefinitionHash : string.Empty).Append('\n');
            builder.Append("acceptedOverrides: ").Append(merge != null ? merge.AcceptedOverrideCount : 0).Append('\n');
            builder.Append("rejectedOverrides: ").Append(merge != null ? merge.RejectedOverrideCount : 0).Append('\n');
            AppendExportIssues(builder, "baseExportIssues", baseExport);
            AppendExportIssues(builder, "overrideExportIssues", overrideExport);
            AppendRows(builder, rows);
            AppendMergeIssues(builder, merge);
            AppendPackageDiagnostics(builder, merge, packageValidation, packageBuildResult);
            AppendCompatibilityDiagnostics(builder, compatibilityReport);
            AppendWarmupDiagnostics(builder, warmup);
            return builder.ToString();
        }

        private static void AppendExportIssues(StringBuilder builder, string title, MxAnimationClipRegistryExportResult export)
        {
            builder.Append(title).Append(":\n");
            if (export == null || export.ValidationReport == null || export.ValidationReport.Issues.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < export.ValidationReport.Issues.Count; i++)
            {
                ResourceCatalogValidationIssue issue = export.ValidationReport.Issues[i];
                builder.Append("- ")
                    .Append(issue.Severity)
                    .Append(' ')
                    .Append(issue.Code)
                    .Append(" key=")
                    .Append(issue.Key)
                    .Append(" message=")
                    .Append(issue.Message)
                    .Append('\n');
            }
        }

        private static void AppendRows(StringBuilder builder, IReadOnlyList<MxAnimationModOverridePreviewRow> rows)
        {
            builder.Append("previewRows:\n");
            if (rows == null || rows.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                MxAnimationModOverridePreviewRow row = rows[i];
                builder.Append("- ")
                    .Append(row.Status)
                    .Append(" category=")
                    .Append(row.Category)
                    .Append(" target=")
                    .Append(row.Target)
                    .Append(" code=")
                    .Append(row.Code)
                    .Append(" field=")
                    .Append(row.Field)
                    .Append(" message=")
                    .Append(row.Message)
                    .Append('\n');
            }
        }

        private static void AppendMergeIssues(StringBuilder builder, MxAnimationModOverrideMergeResult merge)
        {
            builder.Append("mergeIssues:\n");
            if (merge == null || merge.Issues.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < merge.Issues.Count; i++)
            {
                MxAnimationModOverrideIssue issue = merge.Issues[i];
                builder.Append("- ")
                    .Append(issue.Severity)
                    .Append(' ')
                    .Append(issue.Code)
                    .Append(" key=")
                    .Append(issue.Key)
                    .Append(" field=")
                    .Append(issue.Field)
                    .Append(" expected=")
                    .Append(issue.Expected)
                    .Append(" actual=")
                    .Append(issue.Actual)
                    .Append(" message=")
                    .Append(issue.Message)
                    .Append('\n');
            }
        }

        private static void AppendPackageDiagnostics(
            StringBuilder builder,
            MxAnimationModOverrideMergeResult merge,
            MxAnimationPackageValidationReport packageValidation,
            MxAnimationPackageBuildResult packageBuildResult)
        {
            MxAnimationPackageExpectation expectation = merge != null ? merge.MergedPackageExpectation : null;
            builder.Append("packageDiagnostics:\n");
            builder.Append("- packageId: ").Append(expectation != null ? expectation.PackageId : string.Empty).Append('\n');
            builder.Append("- packageVersion: ").Append(expectation != null ? expectation.Version : 0).Append('\n');
            builder.Append("- catalogId: ").Append(expectation != null ? expectation.CatalogId : string.Empty).Append('\n');
            builder.Append("- catalogHash: ").Append(expectation != null ? expectation.CatalogHash : string.Empty).Append('\n');
            builder.Append("- resources: ").Append(expectation != null ? expectation.Resources.Count : 0).Append('\n');
            builder.Append("packageIssues:\n");
            if (packageValidation == null || packageValidation.Issues.Count == 0)
            {
                builder.Append("- none\n");
            }
            else
            {
                for (int i = 0; i < packageValidation.Issues.Count; i++)
                {
                    MxAnimationPackageValidationIssue issue = packageValidation.Issues[i];
                    builder.Append("- ")
                        .Append(issue.Severity)
                        .Append(' ')
                        .Append(issue.Code)
                        .Append(" key=")
                        .Append(issue.Key)
                        .Append(" field=")
                        .Append(issue.Field)
                        .Append(" expected=")
                        .Append(issue.Expected)
                        .Append(" actual=")
                        .Append(issue.Actual)
                        .Append(" message=")
                        .Append(issue.Message)
                        .Append('\n');
                }
            }

            if (packageBuildResult != null && !string.IsNullOrWhiteSpace(packageBuildResult.ReportText))
            {
                builder.Append("packageBuildReport:\n");
                builder.Append(packageBuildResult.ReportText);
                if (!packageBuildResult.ReportText.EndsWith("\n", StringComparison.Ordinal))
                    builder.Append('\n');
            }
        }

        private static void AppendCompatibilityDiagnostics(
            StringBuilder builder,
            MxAnimationCompatibilityWorkstationReport compatibilityReport)
        {
            builder.Append("compatibilityDiagnostics:\n");
            if (compatibilityReport == null)
            {
                builder.Append("- report: missing\n");
                return;
            }

            builder.Append("- success: ").Append(compatibilityReport.Success ? "true" : "false").Append('\n');
            builder.Append("- skeletonProfileId: ")
                .Append(compatibilityReport.Profile != null && compatibilityReport.Profile.SkeletonProfile != null ? compatibilityReport.Profile.SkeletonProfile.ProfileId : string.Empty)
                .Append('\n');
            builder.Append("- skeletonProfileHash: ")
                .Append(compatibilityReport.Profile != null && compatibilityReport.Profile.SkeletonProfile != null ? compatibilityReport.Profile.SkeletonProfile.ProfileHash : string.Empty)
                .Append('\n');
            builder.Append("compatibilityReport:\n");
            builder.Append(compatibilityReport.ReportText);
            if (!compatibilityReport.ReportText.EndsWith("\n", StringComparison.Ordinal))
                builder.Append('\n');
        }

        private static void AppendWarmupDiagnostics(StringBuilder builder, MxAnimationWarmupResult warmup)
        {
            builder.Append("warmupValidation:\n");
            if (warmup == null)
            {
                builder.Append("- not-run\n");
                return;
            }

            builder.Append("- success: ").Append(warmup.Success ? "true" : "false").Append('\n');
            builder.Append("- groupId: ").Append(warmup.GroupId).Append('\n');
            builder.Append("- requiredKeys: ").Append(warmup.RequiredKeys.Count).Append('\n');
            builder.Append("- issues: ").Append(warmup.Issues.Count).Append('\n');
            if (warmup.Issues.Count == 0)
                builder.Append("- none\n");
            for (int i = 0; i < warmup.Issues.Count; i++)
            {
                MxAnimationWarmupIssue issue = warmup.Issues[i];
                builder.Append("- ")
                    .Append(issue.Code)
                    .Append(" key=")
                    .Append(issue.Key)
                    .Append(" field=")
                    .Append(issue.Field)
                    .Append(" expected=")
                    .Append(issue.Expected)
                    .Append(" actual=")
                    .Append(issue.Actual)
                    .Append(" message=")
                    .Append(issue.Message)
                    .Append('\n');
            }
        }
    }
}
