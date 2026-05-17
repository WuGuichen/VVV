using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Resources;

namespace MxFramework.Animation
{
    public sealed class MxAnimationModPackageManifest
    {
        public MxAnimationModPackageManifest(
            string packageId,
            int version,
            string displayName = "",
            string catalogId = "",
            string catalogHash = "",
            int loadOrder = 0)
        {
            PackageId = packageId ?? string.Empty;
            Version = version < 0 ? 0 : version;
            DisplayName = displayName ?? string.Empty;
            CatalogId = catalogId ?? string.Empty;
            CatalogHash = catalogHash ?? string.Empty;
            LoadOrder = loadOrder;
        }

        public string PackageId { get; }
        public int Version { get; }
        public string DisplayName { get; }
        public string CatalogId { get; }
        public string CatalogHash { get; }
        public int LoadOrder { get; }
    }

    public sealed class MxAnimationActionBindingOverride
    {
        public MxAnimationActionBindingOverride(
            MxAnimationActionBinding binding,
            string overrideId = "")
        {
            Binding = binding;
            OverrideId = overrideId ?? string.Empty;
        }

        public string OverrideId { get; }
        public MxAnimationActionBinding Binding { get; }
        public string BindingId => Binding != null ? Binding.BindingId : string.Empty;
        public string ActionKey => Binding != null ? Binding.ActionKey : string.Empty;
    }

    public sealed class MxAnimationLayerDefinitionOverride
    {
        public MxAnimationLayerDefinitionOverride(
            MxAnimationLayerDefinition layer,
            string overrideId = "")
        {
            Layer = layer;
            OverrideId = overrideId ?? string.Empty;
        }

        public string OverrideId { get; }
        public MxAnimationLayerDefinition Layer { get; }
        public MxAnimationLayerId LayerId => Layer != null ? Layer.LayerId : default;
    }

    public sealed class MxAnimationBlend1DDefinitionOverride
    {
        public MxAnimationBlend1DDefinitionOverride(
            MxAnimationBlend1DDefinition blend,
            string overrideId = "")
        {
            Blend = blend;
            OverrideId = overrideId ?? string.Empty;
        }

        public string OverrideId { get; }
        public MxAnimationBlend1DDefinition Blend { get; }
        public string BlendId => Blend != null ? Blend.BlendId : string.Empty;
    }

    public sealed class MxAnimationBlend2DDefinitionOverride
    {
        public MxAnimationBlend2DDefinitionOverride(
            MxAnimationBlend2DDefinition blend,
            string overrideId = "")
        {
            Blend = blend;
            OverrideId = overrideId ?? string.Empty;
        }

        public string OverrideId { get; }
        public MxAnimationBlend2DDefinition Blend { get; }
        public string BlendId => Blend != null ? Blend.BlendId : string.Empty;
    }

    public sealed class MxAnimationModOverrideDefinition
    {
        private readonly List<MxAnimationActionBindingOverride> _actionOverrides;
        private readonly List<MxAnimationLayerDefinitionOverride> _layerOverrides;
        private readonly List<MxAnimationBlend1DDefinitionOverride> _blend1DOverrides;
        private readonly List<MxAnimationBlend2DDefinitionOverride> _blend2DOverrides;
        private readonly List<MxAnimationPackageResourceExpectation> _packageResources;
        private readonly List<string> _acceptedProviderIds;

        public MxAnimationModOverrideDefinition(
            string targetSetId,
            MxAnimationModPackageManifest manifest,
            int overrideVersion,
            int expectedBaseVersion = 0,
            string expectedBaseHash = "",
            int resultVersion = 0,
            IEnumerable<MxAnimationActionBindingOverride> actionOverrides = null,
            IEnumerable<MxAnimationLayerDefinitionOverride> layerOverrides = null,
            IEnumerable<MxAnimationBlend1DDefinitionOverride> blend1DOverrides = null,
            IEnumerable<MxAnimationBlend2DDefinitionOverride> blend2DOverrides = null,
            IEnumerable<MxAnimationPackageResourceExpectation> packageResources = null,
            MxAnimationCompatibilityExpectation compatibilityExpectation = null,
            IEnumerable<string> acceptedProviderIds = null,
            string overrideHash = "")
        {
            TargetSetId = targetSetId ?? string.Empty;
            Manifest = manifest ?? new MxAnimationModPackageManifest(string.Empty, 0);
            OverrideVersion = overrideVersion < 0 ? 0 : overrideVersion;
            ExpectedBaseVersion = expectedBaseVersion < 0 ? 0 : expectedBaseVersion;
            ExpectedBaseHash = expectedBaseHash ?? string.Empty;
            ResultVersion = resultVersion < 0 ? 0 : resultVersion;
            _actionOverrides = actionOverrides != null
                ? new List<MxAnimationActionBindingOverride>(actionOverrides)
                : new List<MxAnimationActionBindingOverride>();
            _layerOverrides = layerOverrides != null
                ? new List<MxAnimationLayerDefinitionOverride>(layerOverrides)
                : new List<MxAnimationLayerDefinitionOverride>();
            _blend1DOverrides = blend1DOverrides != null
                ? new List<MxAnimationBlend1DDefinitionOverride>(blend1DOverrides)
                : new List<MxAnimationBlend1DDefinitionOverride>();
            _blend2DOverrides = blend2DOverrides != null
                ? new List<MxAnimationBlend2DDefinitionOverride>(blend2DOverrides)
                : new List<MxAnimationBlend2DDefinitionOverride>();
            _packageResources = packageResources != null
                ? new List<MxAnimationPackageResourceExpectation>(packageResources)
                : new List<MxAnimationPackageResourceExpectation>();
            CompatibilityExpectation = compatibilityExpectation ?? new MxAnimationCompatibilityExpectation();
            _acceptedProviderIds = acceptedProviderIds != null
                ? new List<string>(acceptedProviderIds)
                : new List<string>();
            OverrideHash = string.IsNullOrWhiteSpace(overrideHash)
                ? MxAnimationModOverrideDefinitionHasher.ComputeHash(this)
                : overrideHash;
        }

        public string TargetSetId { get; }
        public MxAnimationModPackageManifest Manifest { get; }
        public int OverrideVersion { get; }
        public string OverrideHash { get; }
        public int ExpectedBaseVersion { get; }
        public string ExpectedBaseHash { get; }
        public int ResultVersion { get; }
        public IReadOnlyList<MxAnimationActionBindingOverride> ActionOverrides => _actionOverrides;
        public IReadOnlyList<MxAnimationLayerDefinitionOverride> LayerOverrides => _layerOverrides;
        public IReadOnlyList<MxAnimationBlend1DDefinitionOverride> Blend1DOverrides => _blend1DOverrides;
        public IReadOnlyList<MxAnimationBlend2DDefinitionOverride> Blend2DOverrides => _blend2DOverrides;
        public IReadOnlyList<MxAnimationPackageResourceExpectation> PackageResources => _packageResources;
        public MxAnimationCompatibilityExpectation CompatibilityExpectation { get; }
        public IReadOnlyList<string> AcceptedProviderIds => _acceptedProviderIds;
    }

    public enum MxAnimationModOverrideIssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public static class MxAnimationModOverrideIssueCodes
    {
        public const string BaseDefinitionMissing = "BaseDefinitionMissing";
        public const string OverrideDefinitionMissing = "OverrideDefinitionMissing";
        public const string TargetSetIdMismatch = "TargetSetIdMismatch";
        public const string BaseVersionExpectationMissing = "BaseVersionExpectationMissing";
        public const string BaseVersionMismatch = "BaseVersionMismatch";
        public const string BaseHashExpectationMissing = "BaseHashExpectationMissing";
        public const string BaseHashMismatch = "BaseHashMismatch";
        public const string OverrideHashMismatch = "OverrideHashMismatch";
        public const string ActionOverrideInvalid = "ActionOverrideInvalid";
        public const string ActionOverrideTargetMissing = "ActionOverrideTargetMissing";
        public const string LayerOverrideInvalid = "LayerOverrideInvalid";
        public const string Blend1DOverrideInvalid = "Blend1DOverrideInvalid";
        public const string Blend2DOverrideInvalid = "Blend2DOverrideInvalid";
        public const string MappingValidationFailed = "MappingValidationFailed";
        public const string PackageValidationFailed = "PackageValidationFailed";
        public const string CompatibilityValidationFailed = "CompatibilityValidationFailed";
        public const string OverrideAccepted = "OverrideAccepted";
    }

    public sealed class MxAnimationModOverrideIssue
    {
        public MxAnimationModOverrideIssue(
            MxAnimationModOverrideIssueSeverity severity,
            string code,
            ResourceKey key,
            string field,
            string expected,
            string actual,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Key = key;
            Field = field ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MxAnimationModOverrideIssueSeverity Severity { get; }
        public string Code { get; }
        public ResourceKey Key { get; }
        public string Field { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string Message { get; }
    }

    public sealed class MxAnimationModOverrideMergeResult
    {
        private readonly List<MxAnimationModOverrideIssue> _issues;

        public MxAnimationModOverrideMergeResult(
            MxAnimationSetDefinition mergedDefinition,
            MxAnimationPackageExpectation mergedPackageExpectation,
            string baseDefinitionHash,
            int baseVersion,
            string overrideHash,
            int overrideVersion,
            int acceptedOverrideCount,
            int rejectedOverrideCount,
            IEnumerable<MxAnimationModOverrideIssue> issues)
        {
            MergedDefinition = mergedDefinition;
            MergedPackageExpectation = mergedPackageExpectation;
            BaseDefinitionHash = baseDefinitionHash ?? string.Empty;
            BaseVersion = baseVersion;
            OverrideHash = overrideHash ?? string.Empty;
            OverrideVersion = overrideVersion;
            AcceptedOverrideCount = Math.Max(0, acceptedOverrideCount);
            RejectedOverrideCount = Math.Max(0, rejectedOverrideCount);
            _issues = issues != null
                ? new List<MxAnimationModOverrideIssue>(issues)
                : new List<MxAnimationModOverrideIssue>();
        }

        public MxAnimationSetDefinition MergedDefinition { get; }
        public MxAnimationPackageExpectation MergedPackageExpectation { get; }
        public string BaseDefinitionHash { get; }
        public int BaseVersion { get; }
        public string OverrideHash { get; }
        public int OverrideVersion { get; }
        public int AcceptedOverrideCount { get; }
        public int RejectedOverrideCount { get; }
        public IReadOnlyList<MxAnimationModOverrideIssue> Issues => _issues;
        public bool Success
        {
            get
            {
                if (MergedDefinition == null)
                    return false;

                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == MxAnimationModOverrideIssueSeverity.Error)
                        return false;
                }

                return true;
            }
        }
    }

    public sealed class MxAnimationModOverrideMergeRequest
    {
        public MxAnimationModOverrideMergeRequest(
            MxAnimationSetDefinition baseDefinition,
            MxAnimationModOverrideDefinition overrideDefinition,
            ResourceCatalog catalog = null,
            MxAnimationCompatibilityProfile compatibilityProfile = null,
            MxAnimationPackageCatalog packageCatalog = null,
            MxAnimationPackageExpectation basePackageExpectation = null)
        {
            BaseDefinition = baseDefinition;
            OverrideDefinition = overrideDefinition;
            Catalog = catalog;
            CompatibilityProfile = compatibilityProfile;
            PackageCatalog = packageCatalog;
            BasePackageExpectation = basePackageExpectation;
        }

        public MxAnimationSetDefinition BaseDefinition { get; }
        public MxAnimationModOverrideDefinition OverrideDefinition { get; }
        public ResourceCatalog Catalog { get; }
        public MxAnimationCompatibilityProfile CompatibilityProfile { get; }
        public MxAnimationPackageCatalog PackageCatalog { get; }
        public MxAnimationPackageExpectation BasePackageExpectation { get; }
    }

    public static class MxAnimationModOverrideMerger
    {
        public static MxAnimationModOverrideMergeResult Merge(MxAnimationModOverrideMergeRequest request)
        {
            var issues = new List<MxAnimationModOverrideIssue>();
            if (request == null || request.BaseDefinition == null)
            {
                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.BaseDefinitionMissing,
                    default,
                    "baseDefinition",
                    "present",
                    "missing",
                    "Mod animation override requires a base animation set definition."));
                return CreateFailedResult(request, issues);
            }

            if (request.OverrideDefinition == null)
            {
                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.OverrideDefinitionMissing,
                    default,
                    "overrideDefinition",
                    "present",
                    "missing",
                    "Mod animation override definition is missing."));
                return CreateFailedResult(request, issues);
            }

            MxAnimationSetDefinition baseDefinition = request.BaseDefinition;
            MxAnimationModOverrideDefinition overrideDefinition = request.OverrideDefinition;
            ValidateIdentity(baseDefinition, overrideDefinition, issues);

            int rejectedOverrideCount = CountRejectedOverrideInputs(issues);
            if (HasError(issues))
                return CreateFailedResult(request, issues, rejectedOverrideCount);

            int acceptedOverrideCount = 0;
            var actions = new List<MxAnimationActionBinding>(baseDefinition.Actions);
            ApplyActionOverrides(actions, overrideDefinition.ActionOverrides, issues, ref acceptedOverrideCount);
            var layers = new List<MxAnimationLayerDefinition>(baseDefinition.Layers);
            ApplyLayerOverrides(layers, overrideDefinition.LayerOverrides, issues, ref acceptedOverrideCount);
            var blend1DDefinitions = new List<MxAnimationBlend1DDefinition>(baseDefinition.Blend1DDefinitions);
            ApplyBlend1DOverrides(blend1DDefinitions, overrideDefinition.Blend1DOverrides, issues, ref acceptedOverrideCount);
            var blend2DDefinitions = new List<MxAnimationBlend2DDefinition>(baseDefinition.Blend2DDefinitions);
            ApplyBlend2DOverrides(blend2DDefinitions, overrideDefinition.Blend2DOverrides, issues, ref acceptedOverrideCount);

            rejectedOverrideCount += CountRejectedOverrideInputs(issues);
            if (HasError(issues))
                return CreateFailedResult(request, issues, rejectedOverrideCount);

            MxAnimationCompatibilityExpectation compatibilityExpectation = MergeCompatibilityExpectation(
                baseDefinition.CompatibilityExpectation,
                overrideDefinition.CompatibilityExpectation);
            int resultVersion = overrideDefinition.ResultVersion > 0
                ? overrideDefinition.ResultVersion
                : baseDefinition.Version + 1;
            var mergedDefinition = new MxAnimationSetDefinition(
                baseDefinition.SetId,
                resultVersion,
                baseDefinition.DefaultClip,
                baseDefinition.FallbackClip,
                actions,
                baseDefinition.Events,
                layers: layers,
                warmup: baseDefinition.Warmup,
                blend1DDefinitions: blend1DDefinitions,
                blend2DDefinitions: blend2DDefinitions,
                compatibilityExpectation: compatibilityExpectation);

            ValidateMergedDefinition(mergedDefinition, request.Catalog, issues);
            ValidateCompatibility(mergedDefinition, request.CompatibilityProfile, issues);

            MxAnimationPackageExpectation packageExpectation = MergePackageExpectation(
                request.BasePackageExpectation,
                overrideDefinition);
            ValidatePackageExpectation(packageExpectation, request.PackageCatalog, request.Catalog, issues);

            if (HasError(issues))
                return new MxAnimationModOverrideMergeResult(
                    null,
                    packageExpectation,
                    baseDefinition.DefinitionHash,
                    baseDefinition.Version,
                    overrideDefinition.OverrideHash,
                    overrideDefinition.OverrideVersion,
                    acceptedOverrideCount,
                    Math.Max(1, rejectedOverrideCount),
                    issues);

            issues.Add(new MxAnimationModOverrideIssue(
                MxAnimationModOverrideIssueSeverity.Info,
                MxAnimationModOverrideIssueCodes.OverrideAccepted,
                default,
                "override",
                "accepted",
                "accepted",
                "Mod animation override accepted."));
            return new MxAnimationModOverrideMergeResult(
                mergedDefinition,
                packageExpectation,
                baseDefinition.DefinitionHash,
                baseDefinition.Version,
                overrideDefinition.OverrideHash,
                overrideDefinition.OverrideVersion,
                acceptedOverrideCount,
                0,
                issues);
        }

        private static void ValidateIdentity(
            MxAnimationSetDefinition baseDefinition,
            MxAnimationModOverrideDefinition overrideDefinition,
            List<MxAnimationModOverrideIssue> issues)
        {
            if (!string.Equals(baseDefinition.SetId, overrideDefinition.TargetSetId, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.TargetSetIdMismatch,
                    default,
                    "targetSetId",
                    baseDefinition.SetId,
                    overrideDefinition.TargetSetId,
                    "Mod animation override targets a different animation set."));
            }

            if (overrideDefinition.ExpectedBaseVersion <= 0)
            {
                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.BaseVersionExpectationMissing,
                    default,
                    "expectedBaseVersion",
                    "positive",
                    overrideDefinition.ExpectedBaseVersion.ToString(CultureInfo.InvariantCulture),
                    "Mod animation override must pin the base mapping version."));
            }
            else if (overrideDefinition.ExpectedBaseVersion != baseDefinition.Version)
            {
                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.BaseVersionMismatch,
                    default,
                    "baseVersion",
                    overrideDefinition.ExpectedBaseVersion.ToString(CultureInfo.InvariantCulture),
                    baseDefinition.Version.ToString(CultureInfo.InvariantCulture),
                    "Mod animation override was authored for a different base mapping version."));
            }

            if (string.IsNullOrWhiteSpace(overrideDefinition.ExpectedBaseHash))
            {
                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.BaseHashExpectationMissing,
                    default,
                    "expectedBaseHash",
                    "present",
                    "missing",
                    "Mod animation override must pin the base mapping hash."));
            }
            else if (!string.Equals(overrideDefinition.ExpectedBaseHash, baseDefinition.DefinitionHash, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.BaseHashMismatch,
                    default,
                    "baseHash",
                    overrideDefinition.ExpectedBaseHash,
                    baseDefinition.DefinitionHash,
                    "Mod animation override was authored for a different base mapping hash."));
            }

            string actualOverrideHash = MxAnimationModOverrideDefinitionHasher.ComputeHash(overrideDefinition);
            if (!string.Equals(overrideDefinition.OverrideHash, actualOverrideHash, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.OverrideHashMismatch,
                    default,
                    "overrideHash",
                    overrideDefinition.OverrideHash,
                    actualOverrideHash,
                    "Mod animation override hash does not match its canonical content."));
            }
        }

        private static void ApplyActionOverrides(
            List<MxAnimationActionBinding> actions,
            IReadOnlyList<MxAnimationActionBindingOverride> overrides,
            List<MxAnimationModOverrideIssue> issues,
            ref int acceptedOverrideCount)
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                MxAnimationActionBindingOverride actionOverride = overrides[i];
                if (actionOverride == null || actionOverride.Binding == null)
                {
                    issues.Add(Error(
                        MxAnimationModOverrideIssueCodes.ActionOverrideInvalid,
                        default,
                        "actionOverride",
                        "binding",
                        "missing",
                        "Mod animation action override must provide a replacement binding."));
                    continue;
                }

                int index = FindActionIndex(actions, actionOverride.BindingId, actionOverride.ActionKey);
                if (index < 0)
                {
                    issues.Add(Error(
                        MxAnimationModOverrideIssueCodes.ActionOverrideTargetMissing,
                        actionOverride.Binding.Clip,
                        "actionOverride",
                        "existing binding/action",
                        actionOverride.BindingId + "|" + actionOverride.ActionKey,
                        "Mod animation action override target does not exist in the base mapping."));
                    continue;
                }

                actions[index] = actionOverride.Binding;
                acceptedOverrideCount++;
            }
        }

        private static void ApplyLayerOverrides(
            List<MxAnimationLayerDefinition> layers,
            IReadOnlyList<MxAnimationLayerDefinitionOverride> overrides,
            List<MxAnimationModOverrideIssue> issues,
            ref int acceptedOverrideCount)
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                MxAnimationLayerDefinitionOverride layerOverride = overrides[i];
                if (layerOverride == null || layerOverride.Layer == null)
                {
                    issues.Add(Error(
                        MxAnimationModOverrideIssueCodes.LayerOverrideInvalid,
                        default,
                        "layerOverride",
                        "layer",
                        "missing",
                        "Mod animation layer override must provide a layer definition."));
                    continue;
                }

                int index = FindLayerIndex(layers, layerOverride.LayerId);
                if (index >= 0)
                    layers[index] = layerOverride.Layer;
                else
                    layers.Add(layerOverride.Layer);

                acceptedOverrideCount++;
            }
        }

        private static void ApplyBlend1DOverrides(
            List<MxAnimationBlend1DDefinition> blends,
            IReadOnlyList<MxAnimationBlend1DDefinitionOverride> overrides,
            List<MxAnimationModOverrideIssue> issues,
            ref int acceptedOverrideCount)
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                MxAnimationBlend1DDefinitionOverride blendOverride = overrides[i];
                if (blendOverride == null || blendOverride.Blend == null || string.IsNullOrWhiteSpace(blendOverride.Blend.BlendId))
                {
                    issues.Add(Error(
                        MxAnimationModOverrideIssueCodes.Blend1DOverrideInvalid,
                        default,
                        "blend1DOverride",
                        "blend",
                        "missing",
                        "Mod animation 1D blend override must provide a blend definition and id."));
                    continue;
                }

                int index = FindBlend1DIndex(blends, blendOverride.Blend.BlendId);
                if (index >= 0)
                    blends[index] = blendOverride.Blend;
                else
                    blends.Add(blendOverride.Blend);

                acceptedOverrideCount++;
            }
        }

        private static void ApplyBlend2DOverrides(
            List<MxAnimationBlend2DDefinition> blends,
            IReadOnlyList<MxAnimationBlend2DDefinitionOverride> overrides,
            List<MxAnimationModOverrideIssue> issues,
            ref int acceptedOverrideCount)
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                MxAnimationBlend2DDefinitionOverride blendOverride = overrides[i];
                if (blendOverride == null || blendOverride.Blend == null || string.IsNullOrWhiteSpace(blendOverride.Blend.BlendId))
                {
                    issues.Add(Error(
                        MxAnimationModOverrideIssueCodes.Blend2DOverrideInvalid,
                        default,
                        "blend2DOverride",
                        "blend",
                        "missing",
                        "Mod animation 2D blend override must provide a blend definition and id."));
                    continue;
                }

                int index = FindBlend2DIndex(blends, blendOverride.Blend.BlendId);
                if (index >= 0)
                    blends[index] = blendOverride.Blend;
                else
                    blends.Add(blendOverride.Blend);

                acceptedOverrideCount++;
            }
        }

        private static void ValidateMergedDefinition(
            MxAnimationSetDefinition mergedDefinition,
            ResourceCatalog catalog,
            List<MxAnimationModOverrideIssue> issues)
        {
            ResourceCatalogValidationReport report = MxAnimationSetDefinitionValidator.Validate(
                mergedDefinition,
                catalog,
                requireCatalog: true);
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ResourceCatalogValidationIssue issue = report.Issues[i];
                if (issue.Severity != ResourceCatalogValidationSeverity.Error)
                    continue;

                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.MappingValidationFailed,
                    issue.Key,
                    issue.Code,
                    "valid",
                    "invalid",
                    issue.Message));
            }
        }

        private static void ValidateCompatibility(
            MxAnimationSetDefinition mergedDefinition,
            MxAnimationCompatibilityProfile compatibilityProfile,
            List<MxAnimationModOverrideIssue> issues)
        {
            if (compatibilityProfile == null)
            {
                if (mergedDefinition.CompatibilityExpectation != null && !mergedDefinition.CompatibilityExpectation.IsDefault)
                {
                    issues.Add(Error(
                        MxAnimationModOverrideIssueCodes.CompatibilityValidationFailed,
                        default,
                        "compatibilityProfile",
                        "present",
                        "missing",
                        "Compatibility profile is required before applying a Mod animation override."));
                }

                return;
            }

            MxAnimationCompatibilityValidationReport report = MxAnimationCompatibilityValidator.Validate(
                compatibilityProfile,
                mergedDefinition.CompatibilityExpectation);
            for (int i = 0; i < report.Issues.Count; i++)
            {
                MxAnimationCompatibilityIssue issue = report.Issues[i];
                if (issue.Severity != MxAnimationCompatibilityIssueSeverity.Error)
                    continue;

                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.CompatibilityValidationFailed,
                    issue.Key,
                    issue.Code,
                    issue.Expected,
                    issue.Actual,
                    issue.Message));
            }
        }

        private static void ValidatePackageExpectation(
            MxAnimationPackageExpectation packageExpectation,
            MxAnimationPackageCatalog packageCatalog,
            ResourceCatalog catalog,
            List<MxAnimationModOverrideIssue> issues)
        {
            if (packageExpectation == null || packageExpectation.IsDefault)
                return;

            MxAnimationPackageCatalog resolvedPackageCatalog = packageCatalog ?? new MxAnimationPackageCatalog(catalog);
            MxAnimationPackageValidationReport report = MxAnimationPackageCatalogValidator.Validate(
                resolvedPackageCatalog,
                packageExpectation);
            for (int i = 0; i < report.Issues.Count; i++)
            {
                MxAnimationPackageValidationIssue issue = report.Issues[i];
                if (issue.Severity != MxAnimationPackageValidationIssueSeverity.Error)
                    continue;

                issues.Add(Error(
                    MxAnimationModOverrideIssueCodes.PackageValidationFailed,
                    issue.Key,
                    issue.Code,
                    issue.Expected,
                    issue.Actual,
                    issue.Message));
            }
        }

        private static MxAnimationPackageExpectation MergePackageExpectation(
            MxAnimationPackageExpectation baseExpectation,
            MxAnimationModOverrideDefinition overrideDefinition)
        {
            var providers = new List<string>();
            var providerSet = new HashSet<string>(StringComparer.Ordinal);
            if (baseExpectation != null)
                AddProviders(baseExpectation.AcceptedProviderIds, providers, providerSet);
            AddProviders(overrideDefinition.AcceptedProviderIds, providers, providerSet);

            var resources = new List<MxAnimationPackageResourceExpectation>();
            var resourceIndex = new Dictionary<ResourceKey, int>();
            if (baseExpectation != null)
                AddResources(baseExpectation.Resources, resources, resourceIndex);
            AddResources(overrideDefinition.PackageResources, resources, resourceIndex);

            string packageId = !string.IsNullOrWhiteSpace(overrideDefinition.Manifest.PackageId)
                ? overrideDefinition.Manifest.PackageId
                : baseExpectation != null ? baseExpectation.PackageId : string.Empty;
            int version = overrideDefinition.Manifest.Version > 0
                ? overrideDefinition.Manifest.Version
                : baseExpectation != null ? baseExpectation.Version : 0;
            string catalogId = !string.IsNullOrWhiteSpace(overrideDefinition.Manifest.CatalogId)
                ? overrideDefinition.Manifest.CatalogId
                : baseExpectation != null ? baseExpectation.CatalogId : string.Empty;
            string catalogHash = !string.IsNullOrWhiteSpace(overrideDefinition.Manifest.CatalogHash)
                ? overrideDefinition.Manifest.CatalogHash
                : baseExpectation != null ? baseExpectation.CatalogHash : string.Empty;

            return new MxAnimationPackageExpectation(
                packageId,
                version,
                catalogId,
                catalogHash,
                providers,
                resources);
        }

        private static MxAnimationCompatibilityExpectation MergeCompatibilityExpectation(
            MxAnimationCompatibilityExpectation baseExpectation,
            MxAnimationCompatibilityExpectation overrideExpectation)
        {
            baseExpectation = baseExpectation ?? new MxAnimationCompatibilityExpectation();
            overrideExpectation = overrideExpectation ?? new MxAnimationCompatibilityExpectation();

            var bones = new List<string>();
            var sockets = new List<string>();
            AddStrings(baseExpectation.RequiredBonePaths, bones);
            AddStrings(overrideExpectation.RequiredBonePaths, bones);
            AddStrings(baseExpectation.RequiredSocketPaths, sockets);
            AddStrings(overrideExpectation.RequiredSocketPaths, sockets);

            var clips = new List<MxAnimationClipCompatibilityExpectation>();
            AddClipExpectations(baseExpectation.ClipExpectations, clips);
            AddClipExpectations(overrideExpectation.ClipExpectations, clips);
            var masks = new List<MxAnimationAvatarMaskCompatibilityExpectation>();
            AddMaskExpectations(baseExpectation.AvatarMaskExpectations, masks);
            AddMaskExpectations(overrideExpectation.AvatarMaskExpectations, masks);

            string skeletonId = !string.IsNullOrWhiteSpace(overrideExpectation.SkeletonProfileId)
                ? overrideExpectation.SkeletonProfileId
                : baseExpectation.SkeletonProfileId;
            string skeletonHash = !string.IsNullOrWhiteSpace(overrideExpectation.SkeletonProfileHash)
                ? overrideExpectation.SkeletonProfileHash
                : baseExpectation.SkeletonProfileHash;

            return new MxAnimationCompatibilityExpectation(
                skeletonId,
                skeletonHash,
                bones,
                sockets,
                clips,
                masks);
        }

        private static void AddProviders(
            IReadOnlyList<string> source,
            List<string> providers,
            HashSet<string> providerSet)
        {
            for (int i = 0; i < source.Count; i++)
            {
                string provider = source[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(provider) || !providerSet.Add(provider))
                    continue;

                providers.Add(provider);
            }
        }

        private static void AddResources(
            IReadOnlyList<MxAnimationPackageResourceExpectation> source,
            List<MxAnimationPackageResourceExpectation> resources,
            Dictionary<ResourceKey, int> resourceIndex)
        {
            for (int i = 0; i < source.Count; i++)
            {
                MxAnimationPackageResourceExpectation resource = source[i];
                if (resource == null || !resource.Key.IsValid)
                    continue;

                if (resourceIndex.TryGetValue(resource.Key, out int index))
                    resources[index] = resource;
                else
                {
                    resourceIndex.Add(resource.Key, resources.Count);
                    resources.Add(resource);
                }
            }
        }

        private static void AddStrings(IReadOnlyList<string> source, List<string> target)
        {
            var set = new HashSet<string>(target, StringComparer.Ordinal);
            for (int i = 0; i < source.Count; i++)
            {
                string value = source[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value) || !set.Add(value))
                    continue;

                target.Add(value);
            }
        }

        private static void AddClipExpectations(
            IReadOnlyList<MxAnimationClipCompatibilityExpectation> source,
            List<MxAnimationClipCompatibilityExpectation> target)
        {
            var keys = new HashSet<ResourceKey>();
            for (int i = 0; i < target.Count; i++)
                keys.Add(target[i].ClipKey);

            for (int i = 0; i < source.Count; i++)
            {
                MxAnimationClipCompatibilityExpectation expectation = source[i];
                if (expectation == null || !expectation.ClipKey.IsValid || !keys.Add(expectation.ClipKey))
                    continue;

                target.Add(expectation);
            }
        }

        private static void AddMaskExpectations(
            IReadOnlyList<MxAnimationAvatarMaskCompatibilityExpectation> source,
            List<MxAnimationAvatarMaskCompatibilityExpectation> target)
        {
            var keys = new HashSet<ResourceKey>();
            for (int i = 0; i < target.Count; i++)
                keys.Add(target[i].AvatarMaskKey);

            for (int i = 0; i < source.Count; i++)
            {
                MxAnimationAvatarMaskCompatibilityExpectation expectation = source[i];
                if (expectation == null || !expectation.AvatarMaskKey.IsValid || !keys.Add(expectation.AvatarMaskKey))
                    continue;

                target.Add(expectation);
            }
        }

        private static int FindActionIndex(List<MxAnimationActionBinding> actions, string bindingId, string actionKey)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                MxAnimationActionBinding binding = actions[i];
                if (binding == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(bindingId)
                    && string.Equals(binding.BindingId, bindingId, StringComparison.Ordinal))
                {
                    return i;
                }

                if (!string.IsNullOrWhiteSpace(actionKey)
                    && string.Equals(binding.ActionKey, actionKey, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindLayerIndex(List<MxAnimationLayerDefinition> layers, MxAnimationLayerId layerId)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] != null && layers[i].LayerId == layerId)
                    return i;
            }

            return -1;
        }

        private static int FindBlend1DIndex(List<MxAnimationBlend1DDefinition> blends, string blendId)
        {
            for (int i = 0; i < blends.Count; i++)
            {
                if (blends[i] != null && string.Equals(blends[i].BlendId, blendId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static int FindBlend2DIndex(List<MxAnimationBlend2DDefinition> blends, string blendId)
        {
            for (int i = 0; i < blends.Count; i++)
            {
                if (blends[i] != null && string.Equals(blends[i].BlendId, blendId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static bool HasError(IReadOnlyList<MxAnimationModOverrideIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == MxAnimationModOverrideIssueSeverity.Error)
                    return true;
            }

            return false;
        }

        private static int CountRejectedOverrideInputs(IReadOnlyList<MxAnimationModOverrideIssue> issues)
        {
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == MxAnimationModOverrideIssueSeverity.Error)
                    count++;
            }

            return count;
        }

        private static MxAnimationModOverrideMergeResult CreateFailedResult(
            MxAnimationModOverrideMergeRequest request,
            List<MxAnimationModOverrideIssue> issues,
            int rejectedOverrideCount = 1)
        {
            MxAnimationSetDefinition baseDefinition = request != null ? request.BaseDefinition : null;
            MxAnimationModOverrideDefinition overrideDefinition = request != null ? request.OverrideDefinition : null;
            return new MxAnimationModOverrideMergeResult(
                null,
                null,
                baseDefinition != null ? baseDefinition.DefinitionHash : string.Empty,
                baseDefinition != null ? baseDefinition.Version : 0,
                overrideDefinition != null ? overrideDefinition.OverrideHash : string.Empty,
                overrideDefinition != null ? overrideDefinition.OverrideVersion : 0,
                0,
                Math.Max(1, rejectedOverrideCount),
                issues);
        }

        private static MxAnimationModOverrideIssue Error(
            string code,
            ResourceKey key,
            string field,
            string expected,
            string actual,
            string message)
        {
            return new MxAnimationModOverrideIssue(
                MxAnimationModOverrideIssueSeverity.Error,
                code,
                key,
                field,
                expected,
                actual,
                message);
        }
    }

    public static class MxAnimationModOverrideDefinitionHasher
    {
        public const string HashPrefix = "sha256:";

        public static string ComputeHash(MxAnimationModOverrideDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string canonical = CreateCanonicalText(definition);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(canonical);
                byte[] hash = sha256.ComputeHash(bytes);
                return HashPrefix + ToHex(hash);
            }
        }

        public static string CreateCanonicalText(MxAnimationModOverrideDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append("mxanimation.mod.override.v1").Append('\n');
            builder.Append("target=").Append(definition.TargetSetId).Append('\n');
            builder.Append("version=").Append(definition.OverrideVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("expectedBaseVersion=").Append(definition.ExpectedBaseVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("expectedBaseHash=").Append(definition.ExpectedBaseHash).Append('\n');
            builder.Append("resultVersion=").Append(definition.ResultVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            AppendManifest(builder, definition.Manifest);
            AppendActions(builder, definition.ActionOverrides);
            AppendLayers(builder, definition.LayerOverrides);
            AppendBlend1D(builder, definition.Blend1DOverrides);
            AppendBlend2D(builder, definition.Blend2DOverrides);
            AppendPackageResources(builder, definition.PackageResources);
            AppendCompatibility(builder, definition.CompatibilityExpectation);
            AppendProviders(builder, definition.AcceptedProviderIds);
            return builder.ToString();
        }

        private static void AppendManifest(StringBuilder builder, MxAnimationModPackageManifest manifest)
        {
            if (manifest == null)
                return;

            builder.Append("manifest.package=").Append(manifest.PackageId).Append('\n');
            builder.Append("manifest.version=").Append(manifest.Version.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("manifest.display=").Append(manifest.DisplayName).Append('\n');
            builder.Append("manifest.catalog=").Append(manifest.CatalogId).Append('\n');
            builder.Append("manifest.catalogHash=").Append(manifest.CatalogHash).Append('\n');
            builder.Append("manifest.loadOrder=").Append(manifest.LoadOrder.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        private static void AppendActions(StringBuilder builder, IReadOnlyList<MxAnimationActionBindingOverride> overrides)
        {
            var list = new List<MxAnimationActionBindingOverride>(overrides);
            list.Sort((left, right) => string.CompareOrdinal(ActionSortKey(left), ActionSortKey(right)));
            for (int i = 0; i < list.Count; i++)
            {
                MxAnimationActionBindingOverride item = list[i];
                builder.Append("action[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]").Append('\n');
                if (item == null || item.Binding == null)
                {
                    builder.Append("null").Append('\n');
                    continue;
                }

                MxAnimationActionBinding binding = item.Binding;
                builder.Append("id=").Append(item.OverrideId).Append('\n');
                builder.Append("binding=").Append(binding.BindingId).Append('\n');
                builder.Append("action=").Append(binding.ActionKey).Append('\n');
                AppendResourceKey(builder, "clip", binding.Clip);
                builder.Append("layer=").Append(binding.Layer.Value).Append('\n');
                builder.Append("speed=").Append(binding.PlaybackSpeed.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("loop=").Append(binding.Loop ? "1" : "0").Append('\n');
                builder.Append("align=").Append(((int)binding.AlignmentPolicy).ToString(CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("fade=").Append(binding.FadeDurationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                AppendPresentationEvents(builder, "actionEvent", binding.PresentationEvents);
            }
        }

        private static void AppendLayers(StringBuilder builder, IReadOnlyList<MxAnimationLayerDefinitionOverride> overrides)
        {
            var list = new List<MxAnimationLayerDefinitionOverride>(overrides);
            list.Sort((left, right) => string.CompareOrdinal(LayerSortKey(left), LayerSortKey(right)));
            for (int i = 0; i < list.Count; i++)
            {
                MxAnimationLayerDefinitionOverride item = list[i];
                builder.Append("layer[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]").Append('\n');
                if (item == null || item.Layer == null)
                {
                    builder.Append("null").Append('\n');
                    continue;
                }

                MxAnimationLayerDefinition layer = item.Layer;
                builder.Append("id=").Append(item.OverrideId).Append('\n');
                builder.Append("layerId=").Append(layer.LayerId.Value).Append('\n');
                builder.Append("profile=").Append(layer.ProfileId).Append('\n');
                builder.Append("weight=").Append(layer.DefaultWeight.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("blend=").Append(((int)layer.BlendMode).ToString(CultureInfo.InvariantCulture)).Append('\n');
                AppendResourceKey(builder, "mask", layer.AvatarMaskKey);
            }
        }

        private static void AppendBlend1D(StringBuilder builder, IReadOnlyList<MxAnimationBlend1DDefinitionOverride> overrides)
        {
            var list = new List<MxAnimationBlend1DDefinitionOverride>(overrides);
            list.Sort((left, right) => string.CompareOrdinal(Blend1DSortKey(left), Blend1DSortKey(right)));
            for (int i = 0; i < list.Count; i++)
            {
                MxAnimationBlend1DDefinitionOverride item = list[i];
                builder.Append("blend1d[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]").Append('\n');
                if (item == null || item.Blend == null)
                {
                    builder.Append("null").Append('\n');
                    continue;
                }

                MxAnimationBlend1DDefinition blend = item.Blend;
                builder.Append("id=").Append(item.OverrideId).Append('\n');
                builder.Append("blend=").Append(blend.BlendId).Append('\n');
                builder.Append("param=").Append(blend.ParameterId).Append('\n');
                builder.Append("layer=").Append(blend.LayerId.Value).Append('\n');
                builder.Append("scale=").Append(blend.ParameterScale.ToString(CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("fade=").Append(blend.FadeDurationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                for (int pointIndex = 0; pointIndex < blend.Points.Count; pointIndex++)
                {
                    MxAnimationBlend1DPoint point = blend.Points[pointIndex];
                    builder.Append("point[").Append(pointIndex.ToString(CultureInfo.InvariantCulture)).Append("]=");
                    if (point == null)
                    {
                        builder.Append("null").Append('\n');
                        continue;
                    }

                    builder.Append(point.Threshold.ToString(CultureInfo.InvariantCulture)).Append('|');
                    builder.Append(point.PlaybackSpeed.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                    builder.Append(point.Loop ? "1" : "0").Append('\n');
                    AppendResourceKey(builder, "point[" + pointIndex.ToString(CultureInfo.InvariantCulture) + "].clip", point.ClipKey);
                }
            }
        }

        private static void AppendBlend2D(StringBuilder builder, IReadOnlyList<MxAnimationBlend2DDefinitionOverride> overrides)
        {
            var list = new List<MxAnimationBlend2DDefinitionOverride>(overrides);
            list.Sort((left, right) => string.CompareOrdinal(Blend2DSortKey(left), Blend2DSortKey(right)));
            for (int i = 0; i < list.Count; i++)
            {
                MxAnimationBlend2DDefinitionOverride item = list[i];
                builder.Append("blend2d[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]").Append('\n');
                if (item == null || item.Blend == null)
                {
                    builder.Append("null").Append('\n');
                    continue;
                }

                MxAnimationBlend2DDefinition blend = item.Blend;
                builder.Append("id=").Append(item.OverrideId).Append('\n');
                builder.Append("blend=").Append(blend.BlendId).Append('\n');
                builder.Append("paramX=").Append(blend.ParameterXId).Append('\n');
                builder.Append("paramY=").Append(blend.ParameterYId).Append('\n');
                builder.Append("layer=").Append(blend.LayerId.Value).Append('\n');
                builder.Append("scaleX=").Append(blend.ParameterXScale.ToString(CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("scaleY=").Append(blend.ParameterYScale.ToString(CultureInfo.InvariantCulture)).Append('\n');
                builder.Append("fade=").Append(blend.FadeDurationSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                for (int pointIndex = 0; pointIndex < blend.Points.Count; pointIndex++)
                {
                    MxAnimationBlend2DPoint point = blend.Points[pointIndex];
                    builder.Append("point[").Append(pointIndex.ToString(CultureInfo.InvariantCulture)).Append("]=");
                    if (point == null)
                    {
                        builder.Append("null").Append('\n');
                        continue;
                    }

                    builder.Append(point.X.ToString(CultureInfo.InvariantCulture)).Append('|');
                    builder.Append(point.Y.ToString(CultureInfo.InvariantCulture)).Append('|');
                    builder.Append(point.PlaybackSpeed.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                    builder.Append(point.Loop ? "1" : "0").Append('\n');
                    AppendResourceKey(builder, "point[" + pointIndex.ToString(CultureInfo.InvariantCulture) + "].clip", point.ClipKey);
                }
            }
        }

        private static void AppendPackageResources(StringBuilder builder, IReadOnlyList<MxAnimationPackageResourceExpectation> resources)
        {
            var list = new List<MxAnimationPackageResourceExpectation>(resources);
            list.Sort((left, right) => string.CompareOrdinal(ResourceSortKey(left), ResourceSortKey(right)));
            for (int i = 0; i < list.Count; i++)
            {
                MxAnimationPackageResourceExpectation resource = list[i];
                builder.Append("resource[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]").Append('\n');
                if (resource == null)
                {
                    builder.Append("null").Append('\n');
                    continue;
                }

                AppendResourceKey(builder, "key", resource.Key);
                builder.Append("hash=").Append(resource.CatalogEntryHash).Append('\n');
                builder.Append("provider=").Append(resource.ProviderId).Append('\n');
                builder.Append("warmup=").Append(resource.RequiredForWarmup ? "1" : "0").Append('\n');
                builder.Append("kind=").Append(((int)resource.Kind).ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        private static void AppendCompatibility(StringBuilder builder, MxAnimationCompatibilityExpectation expectation)
        {
            if (expectation == null || expectation.IsDefault)
                return;

            builder.Append("compat.skeleton=").Append(expectation.SkeletonProfileId).Append('\n');
            builder.Append("compat.skeletonHash=").Append(expectation.SkeletonProfileHash).Append('\n');
            for (int i = 0; i < expectation.RequiredBonePaths.Count; i++)
                builder.Append("compat.bone[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]=").Append(expectation.RequiredBonePaths[i]).Append('\n');
            for (int i = 0; i < expectation.RequiredSocketPaths.Count; i++)
                builder.Append("compat.socket[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]=").Append(expectation.RequiredSocketPaths[i]).Append('\n');
            for (int i = 0; i < expectation.ClipExpectations.Count; i++)
            {
                MxAnimationClipCompatibilityExpectation clip = expectation.ClipExpectations[i];
                string prefix = "compat.clip[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                AppendResourceKey(builder, prefix, clip.ClipKey);
                builder.Append(prefix).Append(".skeleton=").Append(clip.SkeletonProfileId).Append('\n');
                builder.Append(prefix).Append(".skeletonHash=").Append(clip.SkeletonProfileHash).Append('\n');
                for (int pathIndex = 0; pathIndex < clip.RequiredBindingPaths.Count; pathIndex++)
                {
                    builder.Append(prefix)
                        .Append(".binding[")
                        .Append(pathIndex.ToString(CultureInfo.InvariantCulture))
                        .Append("]=")
                        .Append(clip.RequiredBindingPaths[pathIndex])
                        .Append('\n');
                }
            }
            for (int i = 0; i < expectation.AvatarMaskExpectations.Count; i++)
            {
                MxAnimationAvatarMaskCompatibilityExpectation mask = expectation.AvatarMaskExpectations[i];
                string prefix = "compat.mask[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                AppendResourceKey(builder, prefix, mask.AvatarMaskKey);
                builder.Append(prefix).Append(".skeleton=").Append(mask.SkeletonProfileId).Append('\n');
                builder.Append(prefix).Append(".skeletonHash=").Append(mask.SkeletonProfileHash).Append('\n');
                for (int pathIndex = 0; pathIndex < mask.RequiredActivePaths.Count; pathIndex++)
                {
                    builder.Append(prefix)
                        .Append(".active[")
                        .Append(pathIndex.ToString(CultureInfo.InvariantCulture))
                        .Append("]=")
                        .Append(mask.RequiredActivePaths[pathIndex])
                        .Append('\n');
                }
            }
        }

        private static void AppendPresentationEvents(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<MxAnimationPresentationEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                MxAnimationPresentationEvent animationEvent = events[i];
                string eventPrefix = prefix + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                builder.Append(eventPrefix).Append('\n');
                if (animationEvent == null)
                {
                    builder.Append("null").Append('\n');
                    continue;
                }

                builder.Append(eventPrefix).Append(".id=").Append(animationEvent.EventId).Append('\n');
                builder.Append(eventPrefix).Append(".domain=").Append(((int)animationEvent.TimeDomain).ToString(CultureInfo.InvariantCulture)).Append('\n');
                builder.Append(eventPrefix).Append(".time=").Append(animationEvent.Time.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                builder.Append(eventPrefix).Append(".kind=").Append(animationEvent.EventKind).Append('\n');
                AppendResourceKey(builder, eventPrefix + ".payload", animationEvent.PayloadKey);
                builder.Append(eventPrefix).Append(".socket=").Append(animationEvent.Socket).Append('\n');
                builder.Append(eventPrefix).Append(".tag=").Append(animationEvent.Tag).Append('\n');
                builder.Append(eventPrefix).Append(".replay=").Append(((int)animationEvent.ReplayPolicy).ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        private static void AppendProviders(StringBuilder builder, IReadOnlyList<string> providers)
        {
            var list = new List<string>(providers);
            list.Sort(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++)
                builder.Append("provider[").Append(i.ToString(CultureInfo.InvariantCulture)).Append("]=").Append(list[i] ?? string.Empty).Append('\n');
        }

        private static void AppendResourceKey(StringBuilder builder, string name, ResourceKey key)
        {
            if (!string.IsNullOrWhiteSpace(name))
                builder.Append(name).Append('.');
            builder.Append("id=").Append(key.Id ?? string.Empty).Append('\n');
            if (!string.IsNullOrWhiteSpace(name))
                builder.Append(name).Append('.');
            builder.Append("type=").Append(key.TypeId ?? string.Empty).Append('\n');
            if (!string.IsNullOrWhiteSpace(name))
                builder.Append(name).Append('.');
            builder.Append("variant=").Append(key.Variant ?? string.Empty).Append('\n');
            if (!string.IsNullOrWhiteSpace(name))
                builder.Append(name).Append('.');
            builder.Append("package=").Append(key.PackageId ?? string.Empty).Append('\n');
        }

        private static string ActionSortKey(MxAnimationActionBindingOverride item)
        {
            if (item == null || item.Binding == null)
                return string.Empty;
            return item.BindingId + "|" + item.ActionKey;
        }

        private static string LayerSortKey(MxAnimationLayerDefinitionOverride item)
        {
            return item != null && item.Layer != null ? item.Layer.LayerId.Value : string.Empty;
        }

        private static string Blend1DSortKey(MxAnimationBlend1DDefinitionOverride item)
        {
            return item != null && item.Blend != null ? item.Blend.BlendId : string.Empty;
        }

        private static string Blend2DSortKey(MxAnimationBlend2DDefinitionOverride item)
        {
            return item != null && item.Blend != null ? item.Blend.BlendId : string.Empty;
        }

        private static string ResourceSortKey(MxAnimationPackageResourceExpectation resource)
        {
            return resource != null ? resource.Key.ToString() : string.Empty;
        }

        private static string ToHex(byte[] hash)
        {
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
