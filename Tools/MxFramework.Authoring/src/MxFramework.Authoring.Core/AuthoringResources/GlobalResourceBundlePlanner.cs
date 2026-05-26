using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MxFramework.Authoring
{
    public sealed class GlobalResourceBundlePlannerOptions
    {
        public string BuildTarget { get; set; } = "ActiveBuildTarget";
        public long MaxBundleSizeBytes { get; set; }
    }

    public sealed class GlobalResourceBundlePlan
    {
        public string ProfileId { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public string BuildTarget { get; set; } = string.Empty;
        public List<GlobalResourceBundlePlanBundle> Bundles { get; set; } = new List<GlobalResourceBundlePlanBundle>();
        public List<GlobalResourceBundlePlanEntry> ExternalEntries { get; set; } = new List<GlobalResourceBundlePlanEntry>();
        public List<GlobalResourceBundlePlanEntry> ExcludedEntries { get; set; } = new List<GlobalResourceBundlePlanEntry>();
        public List<GlobalResourceBundlePlanDiagnostic> Diagnostics { get; set; } = new List<GlobalResourceBundlePlanDiagnostic>();
    }

    public sealed class GlobalResourceBundlePlanBundle
    {
        public string BundleName { get; set; } = string.Empty;
        public string BuildTarget { get; set; } = string.Empty;
        public string Compression { get; set; } = "lz4";
        public string BundleRuleId { get; set; } = string.Empty;
        public string GroupHint { get; set; } = string.Empty;
        public List<GlobalResourceBundlePlanEntry> Entries { get; set; } = new List<GlobalResourceBundlePlanEntry>();
        public List<string> IncludedResourceKeys { get; set; } = new List<string>();
        public List<string> SourceUnityGuids { get; set; } = new List<string>();
        public List<string> SourceUnityAssetPaths { get; set; } = new List<string>();
        public List<string> DependencyBundleNames { get; set; } = new List<string>();
        public long EstimatedSizeBytes { get; set; }
        public List<GlobalResourceBundlePlanDiagnostic> Diagnostics { get; set; } = new List<GlobalResourceBundlePlanDiagnostic>();
    }

    public sealed class GlobalResourceBundlePlanEntry
    {
        public string ResourceKey { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string DeliveryMode { get; set; } = string.Empty;
        public string BundleOverrideMode { get; set; } = string.Empty;
        public string BundleName { get; set; } = string.Empty;
        public string BundleRuleId { get; set; } = string.Empty;
        public string GroupHint { get; set; } = string.Empty;
        public string SourceProviderId { get; set; } = string.Empty;
        public string SourceUnityGuid { get; set; } = string.Empty;
        public string SourceUnityAssetPath { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public long EstimatedSizeBytes { get; set; }
    }

    public sealed class GlobalResourceBundlePlanDiagnostic
    {
        public IssueSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string BundleName { get; set; } = string.Empty;
    }

    public static class GlobalResourceBundlePlannerDiagnosticCodes
    {
        public const string ProfileMissing = "globalResource.bundlePlanner.profileMissing";
        public const string RequiredDomainPlanKeyMissing = "globalResource.bundlePlanner.requiredDomainPlanKeyMissing";
        public const string ExternalProviderSelectedForInternalBundle = "globalResource.bundlePlanner.externalProviderSelectedForInternalBundle";
        public const string BundleRuleMissing = "globalResource.bundlePlanner.bundleRuleMissing";
        public const string BundleRuleEmpty = "globalResource.bundlePlanner.bundleRuleEmpty";
        public const string BundleSizeLimitExceeded = "globalResource.bundlePlanner.bundleSizeLimitExceeded";
    }

    public sealed class GlobalResourceBundlePlanner
    {
        private static readonly HashSet<string> InternalSourceProviders = new HashSet<string>(StringComparer.Ordinal)
        {
            string.Empty,
            AuthoringResourceProviderIds.UnityAssetDatabase,
            AuthoringResourceProviderIds.UnityProjectAssets,
            AuthoringResourceProviderIds.GeneratedAssets,
            AuthoringResourceProviderIds.GlobalResourceBuildProfile
        };

        public GlobalResourceBundlePlan Plan(
            GlobalResourceBuildProfile profile,
            AuthoringResourceCollection resources = null,
            GlobalResourceBundlePlannerOptions options = null)
        {
            options = options ?? new GlobalResourceBundlePlannerOptions();
            var plan = new GlobalResourceBundlePlan
            {
                ProfileId = profile != null ? profile.ProfileId : string.Empty,
                CatalogId = profile != null ? profile.CatalogId : string.Empty,
                BuildTarget = string.IsNullOrWhiteSpace(options.BuildTarget) ? "ActiveBuildTarget" : options.BuildTarget
            };

            if (profile == null)
            {
                AddDiagnostic(plan.Diagnostics, IssueSeverity.Error, GlobalResourceBundlePlannerDiagnosticCodes.ProfileMissing, "Global resource build profile is missing.", string.Empty, string.Empty);
                return plan;
            }

            Dictionary<string, AuthoringResourceItem> resourcesByKey = BuildResourcesByRuntimeKey(resources);
            Dictionary<string, GlobalResourceBuildProfileBundleRule> rulesById = BuildRulesById(profile);
            var bundleMap = new Dictionary<string, GlobalResourceBundlePlanBundle>(StringComparer.Ordinal);
            var entryBundleMap = new Dictionary<string, string>(StringComparer.Ordinal);
            AddDeclaredBundles(profile, bundleMap, rulesById, plan.BuildTarget);

            List<GlobalResourceBuildProfileEntry> entries = profile.Entries
                .Where(entry => entry != null)
                .OrderBy(entry => FormatKey(entry.ResourceKey, profile.PackageId), StringComparer.Ordinal)
                .ToList();

            foreach (GlobalResourceBuildProfileEntry entry in entries)
            {
                GlobalResourceBundlePlanEntry planEntry = CreatePlanEntry(entry, profile.PackageId, resourcesByKey);
                string deliveryMode = EffectiveDeliveryMode(entry);
                string overrideMode = EffectiveOverrideMode(entry);
                planEntry.DeliveryMode = deliveryMode;
                planEntry.BundleOverrideMode = overrideMode;

                if (entry.EditorOnly || !entry.RuntimeLoadable)
                {
                    planEntry.Reason = "legacy-excluded";
                    plan.ExcludedEntries.Add(planEntry);
                    continue;
                }

                if (string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.Exclude, StringComparison.Ordinal))
                {
                    planEntry.Reason = "excluded";
                    plan.ExcludedEntries.Add(planEntry);
                    continue;
                }

                if (string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceExternal, StringComparison.Ordinal))
                {
                    planEntry.Reason = "external";
                    plan.ExternalEntries.Add(planEntry);
                    continue;
                }

                if (string.Equals(deliveryMode, GlobalResourceBuildProfileDeliveryModes.Excluded, StringComparison.Ordinal) ||
                    string.Equals(deliveryMode, GlobalResourceBuildProfileDeliveryModes.EditorOnly, StringComparison.Ordinal))
                {
                    planEntry.Reason = "delivery-excluded";
                    plan.ExcludedEntries.Add(planEntry);
                    continue;
                }

                bool forcedInternal =
                    string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceStandalone, StringComparison.Ordinal) ||
                    string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceBundle, StringComparison.Ordinal);
                if (!forcedInternal && string.Equals(deliveryMode, GlobalResourceBuildProfileDeliveryModes.External, StringComparison.Ordinal))
                {
                    planEntry.Reason = "external";
                    plan.ExternalEntries.Add(planEntry);
                    continue;
                }

                if (!InternalSourceProviders.Contains(planEntry.SourceProviderId))
                {
                    AddDiagnostic(
                        plan.Diagnostics,
                        IssueSeverity.Error,
                        GlobalResourceBundlePlannerDiagnosticCodes.ExternalProviderSelectedForInternalBundle,
                        "External provider resource was selected for an internal bundle.",
                        planEntry.ResourceKey,
                        string.Empty);
                    planEntry.Reason = "external-provider";
                    plan.ExternalEntries.Add(planEntry);
                    continue;
                }

                if (string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceStandalone, StringComparison.Ordinal) ||
                    string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceBundle, StringComparison.Ordinal))
                {
                    BundleChoice forcedChoice = ChooseForcedBundle(entry, plan.BuildTarget);
                    AddPlannedBundleEntry(planEntry, forcedChoice, bundleMap, entryBundleMap);
                    continue;
                }

                List<BundleChoice> choices = FindMatchingBundles(entry, profile, rulesById, plan.BuildTarget);
                if (choices.Count == 0)
                {
                    AddDiagnostic(
                        plan.Diagnostics,
                        IssueSeverity.Error,
                        GlobalResourceBundlePlannerDiagnosticCodes.BundleRuleMissing,
                        "Internal runtime resource is not assigned to a defined bundle rule.",
                        planEntry.ResourceKey,
                        string.Empty);
                    planEntry.Reason = "missing-bundle-rule";
                    plan.ExcludedEntries.Add(planEntry);
                    continue;
                }

                for (int i = 0; i < choices.Count; i++)
                    AddPlannedBundleEntry(planEntry, choices[i], bundleMap, entryBundleMap);
            }

            AddDependencyBundles(profile, bundleMap, entryBundleMap, rulesById);
            AddEmptyRuleDiagnostics(profile, plan, rulesById, bundleMap);
            AddRequiredDomainPlanDiagnostics(profile, plan, entryBundleMap);

            plan.Bundles = bundleMap.Values.OrderBy(bundle => bundle.BundleName, StringComparer.Ordinal).ToList();
            AddBundleSizeDiagnostics(plan, options);
            foreach (GlobalResourceBundlePlanBundle bundle in plan.Bundles)
            {
                bundle.Entries = bundle.Entries.OrderBy(entry => entry.ResourceKey, StringComparer.Ordinal).ToList();
                bundle.IncludedResourceKeys = bundle.IncludedResourceKeys.OrderBy(key => key, StringComparer.Ordinal).ToList();
                bundle.SourceUnityGuids = bundle.SourceUnityGuids.OrderBy(value => value, StringComparer.Ordinal).ToList();
                bundle.SourceUnityAssetPaths = bundle.SourceUnityAssetPaths.OrderBy(value => value, StringComparer.Ordinal).ToList();
                bundle.DependencyBundleNames = bundle.DependencyBundleNames.OrderBy(value => value, StringComparer.Ordinal).ToList();
                bundle.Diagnostics = SortDiagnostics(bundle.Diagnostics);
            }

            plan.ExternalEntries = plan.ExternalEntries.OrderBy(entry => entry.ResourceKey, StringComparer.Ordinal).ToList();
            plan.ExcludedEntries = plan.ExcludedEntries.OrderBy(entry => entry.ResourceKey, StringComparer.Ordinal).ToList();
            plan.Diagnostics = SortDiagnostics(plan.Diagnostics);
            return plan;
        }

        private static Dictionary<string, AuthoringResourceItem> BuildResourcesByRuntimeKey(AuthoringResourceCollection resources)
        {
            var result = new Dictionary<string, AuthoringResourceItem>(StringComparer.Ordinal);
            if (resources == null || resources.Items == null)
                return result;

            foreach (AuthoringResourceItem item in resources.Items)
            {
                if (item == null || item.ProviderBindings == null)
                    continue;

                foreach (AuthoringResourceProviderBinding binding in item.ProviderBindings)
                {
                    if (binding == null || string.IsNullOrWhiteSpace(binding.RuntimeResourceKey))
                        continue;
                    if (!result.ContainsKey(binding.RuntimeResourceKey))
                        result.Add(binding.RuntimeResourceKey, item);
                }
            }

            return result;
        }

        private static Dictionary<string, GlobalResourceBuildProfileBundleRule> BuildRulesById(GlobalResourceBuildProfile profile)
        {
            var result = new Dictionary<string, GlobalResourceBuildProfileBundleRule>(StringComparer.Ordinal);
            if (profile.BundleRules == null)
                return result;

            foreach (GlobalResourceBuildProfileBundleRule rule in profile.BundleRules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || result.ContainsKey(rule.Id))
                    continue;
                result.Add(rule.Id, rule);
            }

            return result;
        }

        private static GlobalResourceBundlePlanEntry CreatePlanEntry(GlobalResourceBuildProfileEntry entry, string defaultPackageId, Dictionary<string, AuthoringResourceItem> resourcesByKey)
        {
            string key = FormatKey(entry.ResourceKey, defaultPackageId);
            var planEntry = new GlobalResourceBundlePlanEntry
            {
                ResourceKey = key,
                ResourceId = entry.ResourceKey != null ? entry.ResourceKey.Id ?? string.Empty : string.Empty,
                ResourceType = EffectiveType(entry.ResourceKey),
                PackageId = EffectivePackageId(entry.ResourceKey, defaultPackageId),
                Variant = entry.ResourceKey != null ? entry.ResourceKey.Variant ?? string.Empty : string.Empty,
                SourceProviderId = entry.Source != null ? entry.Source.ProviderId ?? string.Empty : string.Empty,
                SourceUnityGuid = entry.Source != null ? entry.Source.UnityGuid ?? string.Empty : string.Empty,
                SourceUnityAssetPath = entry.Source != null ? entry.Source.UnityAssetPath ?? string.Empty : string.Empty,
                EstimatedSizeBytes = ReadEstimatedSize(entry)
            };

            if (resourcesByKey.TryGetValue(key, out AuthoringResourceItem item) && item != null)
            {
                if (planEntry.EstimatedSizeBytes <= 0)
                    planEntry.EstimatedSizeBytes = ReadEstimatedSize(item);
                if (string.IsNullOrWhiteSpace(planEntry.SourceProviderId))
                    planEntry.SourceProviderId = item.SourceProviderId ?? string.Empty;
            }

            return planEntry;
        }

        private static void AddDeclaredBundles(
            GlobalResourceBuildProfile profile,
            Dictionary<string, GlobalResourceBundlePlanBundle> bundleMap,
            Dictionary<string, GlobalResourceBuildProfileBundleRule> rulesById,
            string defaultBuildTarget)
        {
            foreach (GlobalResourceBuildProfileBundleRule rule in rulesById.Values.OrderBy(rule => rule.Id, StringComparer.Ordinal))
            {
                BundleChoice choice = BundleChoiceFromRule(rule, rule.Id, rule.Id, defaultBuildTarget);
                GetOrCreateBundle(bundleMap, choice);
            }
        }

        private static void AddPlannedBundleEntry(
            GlobalResourceBundlePlanEntry planEntry,
            BundleChoice choice,
            Dictionary<string, GlobalResourceBundlePlanBundle> bundleMap,
            Dictionary<string, string> entryBundleMap)
        {
            var bundleEntry = ClonePlanEntry(planEntry);
            bundleEntry.BundleName = choice.BundleName;
            bundleEntry.BundleRuleId = choice.BundleRuleId;
            bundleEntry.GroupHint = choice.GroupHint;
            bundleEntry.Reason = choice.Reason;
            GlobalResourceBundlePlanBundle bundle = GetOrCreateBundle(bundleMap, choice);
            AddBundleEntry(bundle, bundleEntry);
            if (!entryBundleMap.ContainsKey(bundleEntry.ResourceKey))
                entryBundleMap.Add(bundleEntry.ResourceKey, bundle.BundleName);
        }

        private static GlobalResourceBundlePlanEntry ClonePlanEntry(GlobalResourceBundlePlanEntry entry)
        {
            return new GlobalResourceBundlePlanEntry
            {
                ResourceKey = entry.ResourceKey,
                ResourceId = entry.ResourceId,
                ResourceType = entry.ResourceType,
                PackageId = entry.PackageId,
                Variant = entry.Variant,
                DeliveryMode = entry.DeliveryMode,
                BundleOverrideMode = entry.BundleOverrideMode,
                BundleName = entry.BundleName,
                BundleRuleId = entry.BundleRuleId,
                GroupHint = entry.GroupHint,
                SourceProviderId = entry.SourceProviderId,
                SourceUnityGuid = entry.SourceUnityGuid,
                SourceUnityAssetPath = entry.SourceUnityAssetPath,
                Reason = entry.Reason,
                EstimatedSizeBytes = entry.EstimatedSizeBytes
            };
        }

        private static BundleChoice ChooseForcedBundle(
            GlobalResourceBuildProfileEntry entry,
            string defaultBuildTarget)
        {
            string overrideMode = EffectiveOverrideMode(entry);
            if (string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceStandalone, StringComparison.Ordinal))
            {
                string standaloneName = !string.IsNullOrWhiteSpace(entry.BundleOverrideValue)
                    ? SanitizeBundleName(entry.BundleOverrideValue)
                    : "global.standalone." + SanitizeBundleSegment(entry.ResourceKey != null ? entry.ResourceKey.Id : string.Empty);
                return new BundleChoice(standaloneName, string.Empty, "standalone", "forceStandalone", defaultBuildTarget, "lz4");
            }

            return new BundleChoice(SanitizeBundleName(entry.BundleOverrideValue), string.Empty, entry.BundleOverrideValue, "forceBundle", defaultBuildTarget, "lz4");
        }

        private static List<BundleChoice> FindMatchingBundles(
            GlobalResourceBuildProfileEntry entry,
            GlobalResourceBuildProfile profile,
            Dictionary<string, GlobalResourceBuildProfileBundleRule> rulesById,
            string defaultBuildTarget)
        {
            var choices = new List<BundleChoice>();
            if (!string.IsNullOrWhiteSpace(entry.BundleRule))
            {
                if (rulesById.TryGetValue(entry.BundleRule, out GlobalResourceBuildProfileBundleRule directRule))
                    choices.Add(BundleChoiceFromRule(directRule, entry.BundleRule, entry.BundleRule, defaultBuildTarget));
                return choices;
            }

            foreach (GlobalResourceBuildProfileBundleRule rule in rulesById.Values.OrderBy(rule => rule.Id, StringComparer.Ordinal))
            {
                if (RuleMatchesEntry(rule, entry, profile.PackageId))
                    choices.Add(BundleChoiceFromRule(rule, rule.Id, rule.Id, defaultBuildTarget));
            }

            return choices;
        }

        private static BundleChoice BundleChoiceFromRule(GlobalResourceBuildProfileBundleRule rule, string fallbackId, string groupHint, string defaultBuildTarget)
        {
            string ruleId = !string.IsNullOrWhiteSpace(rule.Id) ? rule.Id : fallbackId;
            string bundleName = !string.IsNullOrWhiteSpace(rule.BundleName) ? rule.BundleName : "global." + SanitizeBundleSegment(ruleId);
            string buildTarget = !string.IsNullOrWhiteSpace(rule.BuildTarget) ? rule.BuildTarget : defaultBuildTarget;
            string compression = !string.IsNullOrWhiteSpace(rule.Compression) ? rule.Compression : "lz4";
            return new BundleChoice(bundleName, ruleId, groupHint, "bundleRule", buildTarget, compression);
        }

        private static bool RuleMatchesEntry(GlobalResourceBuildProfileBundleRule rule, GlobalResourceBuildProfileEntry entry, string defaultPackageId)
        {
            if (rule == null || entry == null)
                return false;
            if (!string.IsNullOrWhiteSpace(rule.Id) && string.Equals(entry.BundleRule, rule.Id, StringComparison.Ordinal))
                return true;
            if (ContainsAny(entry.Labels, rule.MatchLabels))
                return true;
            if (Contains(rule.MatchPackageIds, EffectivePackageId(entry.ResourceKey, defaultPackageId)))
                return true;
            if (ContainsAny(entry.Labels, PrefixValues("domain.", rule.MatchDomains)))
                return true;

            string entryKey = CanonicalKey(entry.ResourceKey, defaultPackageId);
            if (rule.ExplicitKeys == null)
                return false;

            for (int i = 0; i < rule.ExplicitKeys.Count; i++)
            {
                if (string.Equals(entryKey, CanonicalKey(rule.ExplicitKeys[i], defaultPackageId), StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static GlobalResourceBundlePlanBundle GetOrCreateBundle(Dictionary<string, GlobalResourceBundlePlanBundle> bundleMap, BundleChoice choice)
        {
            if (bundleMap.TryGetValue(choice.BundleName, out GlobalResourceBundlePlanBundle bundle))
                return bundle;

            bundle = new GlobalResourceBundlePlanBundle
            {
                BundleName = choice.BundleName,
                BuildTarget = choice.BuildTarget,
                Compression = choice.Compression,
                BundleRuleId = choice.BundleRuleId,
                GroupHint = choice.GroupHint
            };
            bundleMap.Add(bundle.BundleName, bundle);
            return bundle;
        }

        private static void AddBundleEntry(GlobalResourceBundlePlanBundle bundle, GlobalResourceBundlePlanEntry entry)
        {
            bundle.Entries.Add(entry);
            AddDistinct(bundle.IncludedResourceKeys, entry.ResourceKey);
            AddDistinct(bundle.SourceUnityGuids, entry.SourceUnityGuid);
            AddDistinct(bundle.SourceUnityAssetPaths, entry.SourceUnityAssetPath);
            if (entry.EstimatedSizeBytes > 0)
                bundle.EstimatedSizeBytes += entry.EstimatedSizeBytes;
        }

        private static void AddDependencyBundles(
            GlobalResourceBuildProfile profile,
            Dictionary<string, GlobalResourceBundlePlanBundle> bundleMap,
            Dictionary<string, string> entryBundleMap,
            Dictionary<string, GlobalResourceBuildProfileBundleRule> rulesById)
        {
            foreach (GlobalResourceBuildProfileEntry entry in profile.Entries)
            {
                if (entry == null || entry.Dependencies == null)
                    continue;

                string sourceKey = FormatKey(entry.ResourceKey, profile.PackageId);
                if (!entryBundleMap.TryGetValue(sourceKey, out string sourceBundleName) || !bundleMap.TryGetValue(sourceBundleName, out GlobalResourceBundlePlanBundle sourceBundle))
                    continue;

                if (!string.IsNullOrWhiteSpace(sourceBundle.BundleRuleId) &&
                    rulesById.TryGetValue(sourceBundle.BundleRuleId, out GlobalResourceBuildProfileBundleRule rule) &&
                    !rule.IncludeDependencies)
                    continue;

                foreach (GlobalResourceBuildProfileResourceKey dependency in entry.Dependencies)
                {
                    string dependencyKey = FormatKey(dependency, profile.PackageId);
                    if (entryBundleMap.TryGetValue(dependencyKey, out string dependencyBundleName) &&
                        !string.Equals(sourceBundleName, dependencyBundleName, StringComparison.Ordinal))
                    {
                        AddDistinct(sourceBundle.DependencyBundleNames, dependencyBundleName);
                    }
                }
            }
        }

        private static void AddEmptyRuleDiagnostics(GlobalResourceBuildProfile profile, GlobalResourceBundlePlan plan, Dictionary<string, GlobalResourceBuildProfileBundleRule> rulesById, Dictionary<string, GlobalResourceBundlePlanBundle> bundleMap)
        {
            foreach (GlobalResourceBuildProfileBundleRule rule in rulesById.Values.OrderBy(rule => rule.Id, StringComparer.Ordinal))
            {
                if (rule.AllowEmpty)
                    continue;

                string expectedName = !string.IsNullOrWhiteSpace(rule.BundleName) ? rule.BundleName : "global." + SanitizeBundleSegment(rule.Id);
                if (!bundleMap.TryGetValue(expectedName, out GlobalResourceBundlePlanBundle bundle) || bundle.Entries.Count == 0)
                {
                    AddDiagnostic(plan.Diagnostics, IssueSeverity.Warning, GlobalResourceBundlePlannerDiagnosticCodes.BundleRuleEmpty, "Bundle rule selects no internal bundle entries.", string.Empty, expectedName);
                }
            }
        }

        private static void AddRequiredDomainPlanDiagnostics(GlobalResourceBuildProfile profile, GlobalResourceBundlePlan plan, Dictionary<string, string> entryBundleMap)
        {
            if (profile.RequiredDomainPlanKeys == null)
                return;

            foreach (GlobalResourceBuildProfileResourceKey key in profile.RequiredDomainPlanKeys)
            {
                string requiredKey = FormatKey(key, profile.PackageId);
                if (!entryBundleMap.ContainsKey(requiredKey))
                {
                    AddDiagnostic(plan.Diagnostics, IssueSeverity.Error, GlobalResourceBundlePlannerDiagnosticCodes.RequiredDomainPlanKeyMissing, "Runtime-required domain plan key is missing from internal bundle output.", requiredKey, string.Empty);
                }
            }
        }

        private static void AddBundleSizeDiagnostics(GlobalResourceBundlePlan plan, GlobalResourceBundlePlannerOptions options)
        {
            if (options == null || options.MaxBundleSizeBytes <= 0)
                return;

            foreach (GlobalResourceBundlePlanBundle bundle in plan.Bundles)
            {
                if (bundle.EstimatedSizeBytes <= options.MaxBundleSizeBytes)
                    continue;

                var diagnostic = new GlobalResourceBundlePlanDiagnostic
                {
                    Severity = IssueSeverity.Warning,
                    Code = GlobalResourceBundlePlannerDiagnosticCodes.BundleSizeLimitExceeded,
                    Message = "Bundle estimated size exceeds configured limit.",
                    BundleName = bundle.BundleName
                };
                bundle.Diagnostics.Add(diagnostic);
                plan.Diagnostics.Add(diagnostic);
            }
        }

        private static long ReadEstimatedSize(GlobalResourceBuildProfileEntry entry)
        {
            if (entry == null || entry.ProviderData == null)
                return 0;

            return ReadEstimatedSize(entry.ProviderData);
        }

        private static long ReadEstimatedSize(AuthoringResourceItem item)
        {
            if (item == null || item.Metadata == null)
                return 0;

            return ReadEstimatedSize(item.Metadata);
        }

        private static long ReadEstimatedSize(Dictionary<string, string> metadata)
        {
            if (metadata == null)
                return 0;

            foreach (string key in new[] { "sizeBytes", "estimatedSizeBytes", "fileSizeBytes" })
            {
                if (metadata.TryGetValue(key, out string value) &&
                    long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
                    return result;
            }

            return 0;
        }

        private static string EffectiveDeliveryMode(GlobalResourceBuildProfileEntry entry)
        {
            if (entry == null)
                return GlobalResourceBuildProfileDeliveryModes.Excluded;
            if (entry.EditorOnly)
                return GlobalResourceBuildProfileDeliveryModes.EditorOnly;
            if (!entry.RuntimeLoadable)
                return GlobalResourceBuildProfileDeliveryModes.Excluded;
            if (!string.IsNullOrWhiteSpace(entry.DeliveryMode))
                return entry.DeliveryMode;

            return GlobalResourceBuildProfileDeliveryModes.Internal;
        }

        private static string EffectiveOverrideMode(GlobalResourceBuildProfileEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.BundleOverrideMode))
                return GlobalResourceBuildProfileBundleOverrideModes.None;

            return entry.BundleOverrideMode;
        }

        private static string FormatKey(GlobalResourceBuildProfileResourceKey key, string defaultPackageId)
        {
            if (key == null)
                return string.Empty;

            return EffectivePackageId(key, defaultPackageId) + ":" + EffectiveType(key) + ":" + (key.Id ?? string.Empty) + ":" + (key.Variant ?? string.Empty);
        }

        private static string CanonicalKey(GlobalResourceBuildProfileResourceKey key, string defaultPackageId)
        {
            if (key == null || string.IsNullOrWhiteSpace(key.Id) || string.IsNullOrWhiteSpace(EffectiveType(key)))
                return string.Empty;

            return EffectivePackageId(key, defaultPackageId) + "|" + EffectiveType(key) + "|" + key.Id + "|" + (key.Variant ?? string.Empty);
        }

        private static string EffectiveType(GlobalResourceBuildProfileResourceKey key)
        {
            if (key == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(key.Type) ? key.Type : (key.TypeId ?? string.Empty);
        }

        private static string EffectivePackageId(GlobalResourceBuildProfileResourceKey key, string defaultPackageId)
        {
            if (key != null && !string.IsNullOrWhiteSpace(key.PackageId))
                return key.PackageId;

            return defaultPackageId ?? string.Empty;
        }

        private static bool Contains(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
                return false;

            return values.Contains(value, StringComparer.Ordinal);
        }

        private static bool ContainsAny(List<string> left, IEnumerable<string> right)
        {
            if (left == null || right == null)
                return false;

            var set = new HashSet<string>(left.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            foreach (string value in right)
            {
                if (!string.IsNullOrWhiteSpace(value) && set.Contains(value))
                    return true;
            }

            return false;
        }

        private static IEnumerable<string> PrefixValues(string prefix, List<string> values)
        {
            if (values == null)
                yield break;

            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    yield return prefix + values[i];
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string FirstLabelWithPrefix(List<string> labels, string prefix)
        {
            if (labels == null)
                return string.Empty;

            return labels.FirstOrDefault(label => !string.IsNullOrWhiteSpace(label) && label.StartsWith(prefix, StringComparison.Ordinal)) ?? string.Empty;
        }

        private static string SanitizeBundleName(string value)
        {
            string sanitized = SanitizeBundleSegment(value);
            return string.IsNullOrWhiteSpace(sanitized) ? "global.misc" : sanitized;
        }

        private static string SanitizeBundleSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "misc";

            var builder = new StringBuilder(value.Length);
            bool lastWasSeparator = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (allowed)
                {
                    builder.Append(c);
                    lastWasSeparator = false;
                    continue;
                }

                if ((c == '.' || c == '_' || c == '-' || char.IsWhiteSpace(c)) && !lastWasSeparator)
                {
                    builder.Append('.');
                    lastWasSeparator = true;
                }
            }

            string result = builder.ToString().Trim('.');
            return string.IsNullOrWhiteSpace(result) ? "misc" : result;
        }

        private static void AddDistinct(List<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || values.Contains(value, StringComparer.Ordinal))
                return;

            values.Add(value);
        }

        private static void AddDiagnostic(List<GlobalResourceBundlePlanDiagnostic> diagnostics, IssueSeverity severity, string code, string message, string resourceKey, string bundleName)
        {
            diagnostics.Add(new GlobalResourceBundlePlanDiagnostic
            {
                Severity = severity,
                Code = code,
                Message = message,
                ResourceKey = resourceKey ?? string.Empty,
                BundleName = bundleName ?? string.Empty
            });
        }

        private static List<GlobalResourceBundlePlanDiagnostic> SortDiagnostics(List<GlobalResourceBundlePlanDiagnostic> diagnostics)
        {
            return diagnostics
                .OrderBy(diagnostic => diagnostic.Severity)
                .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.ResourceKey, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.BundleName, StringComparer.Ordinal)
                .ToList();
        }

        private sealed class BundleChoice
        {
            public BundleChoice(string bundleName, string bundleRuleId, string groupHint, string reason, string buildTarget, string compression)
            {
                BundleName = bundleName;
                BundleRuleId = bundleRuleId ?? string.Empty;
                GroupHint = groupHint ?? string.Empty;
                Reason = reason ?? string.Empty;
                BuildTarget = buildTarget ?? string.Empty;
                Compression = compression ?? string.Empty;
            }

            public string BundleName { get; }
            public string BundleRuleId { get; }
            public string GroupHint { get; }
            public string Reason { get; }
            public string BuildTarget { get; }
            public string Compression { get; }
        }
    }
}
