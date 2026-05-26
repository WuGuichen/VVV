using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class GlobalResourceBuildProfileValidationReport
    {
        public List<GlobalResourceBuildProfileValidationIssue> Issues { get; set; } = new List<GlobalResourceBuildProfileValidationIssue>();

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i].Severity == IssueSeverity.Error)
                        return true;
                }

                return false;
            }
        }
    }

    public sealed class GlobalResourceBuildProfileValidationIssue
    {
        public IssueSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
    }

    public static class GlobalResourceBuildProfileValidationCodes
    {
        public const string DuplicateResourceKey = "globalResource.profile.duplicateResourceKey";
        public const string ResourceKeyIdRequired = "globalResource.profile.resourceKey.idRequired";
        public const string ResourceKeyTypeRequired = "globalResource.profile.resourceKey.typeRequired";
        public const string ResourceKeyInvalidCharacters = "globalResource.profile.resourceKey.invalidCharacters";
        public const string RuntimeUnityGuidRequired = "globalResource.profile.runtimeUnityGuidRequired";
        public const string UnknownCompression = "globalResource.profile.unknownCompression";
        public const string RuntimeBundleRuleRequired = "globalResource.profile.runtimeBundleRuleRequired";
        public const string BundleRuleMissing = "globalResource.profile.bundleRuleMissing";
        public const string BundleRuleEmpty = "globalResource.profile.bundleRuleEmpty";
        public const string PreloadGroupEmpty = "globalResource.profile.preloadGroupEmpty";
        public const string RequiredDomainPlanKeyMissing = "globalResource.profile.requiredDomainPlanKeyMissing";
        public const string UnknownDeliveryMode = "globalResource.profile.unknownDeliveryMode";
        public const string UnknownBundleOverrideMode = "globalResource.profile.unknownBundleOverrideMode";
        public const string BundleOverrideValueRequired = "globalResource.profile.bundleOverrideValueRequired";
        public const string ExternalEntryBundleRuleIgnored = "globalResource.profile.externalEntryBundleRuleIgnored";
    }

    public static class GlobalResourceBuildProfileValidator
    {
        private static readonly HashSet<string> SupportedCompressions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "lz4",
            "uncompressed",
            "lzma"
        };
        private static readonly HashSet<string> SupportedDeliveryModes = new HashSet<string>(StringComparer.Ordinal)
        {
            GlobalResourceBuildProfileDeliveryModes.Internal,
            GlobalResourceBuildProfileDeliveryModes.External,
            GlobalResourceBuildProfileDeliveryModes.EditorOnly,
            GlobalResourceBuildProfileDeliveryModes.Excluded
        };
        private static readonly HashSet<string> SupportedBundleOverrideModes = new HashSet<string>(StringComparer.Ordinal)
        {
            GlobalResourceBuildProfileBundleOverrideModes.None,
            GlobalResourceBuildProfileBundleOverrideModes.ForceBundle,
            GlobalResourceBuildProfileBundleOverrideModes.ForceStandalone,
            GlobalResourceBuildProfileBundleOverrideModes.ForceExternal,
            GlobalResourceBuildProfileBundleOverrideModes.Exclude
        };

        public static GlobalResourceBuildProfileValidationReport Validate(GlobalResourceBuildProfile profile)
        {
            var report = new GlobalResourceBuildProfileValidationReport();
            if (profile == null)
            {
                Add(report, IssueSeverity.Error, "globalResource.profile.missing", "Global resource build profile is missing.", "profile", string.Empty);
                return report;
            }

            Dictionary<string, GlobalResourceBuildProfileBundleRule> rulesById = BuildRulesById(profile);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                GlobalResourceBuildProfileEntry entry = profile.Entries[i];
                GlobalResourceBuildProfileResourceKey key = entry != null ? entry.ResourceKey : null;
                string sourcePath = "entries[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                string displayKey = FormatKey(key, profile.PackageId);
                ValidateKey(report, key, sourcePath + ".resourceKey", displayKey);

                string canonicalKey = CanonicalKey(key, profile.PackageId);
                if (!string.IsNullOrEmpty(canonicalKey) && !keys.Add(canonicalKey))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.DuplicateResourceKey, "Duplicate resource key in global resource build profile.", sourcePath + ".resourceKey", displayKey);
                }

                if (entry == null)
                    continue;

                string deliveryMode = EffectiveDeliveryMode(entry);
                string overrideMode = EffectiveBundleOverrideMode(entry);
                if (!SupportedDeliveryModes.Contains(deliveryMode))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.UnknownDeliveryMode, "Unknown deliveryMode value '" + entry.DeliveryMode + "'.", sourcePath + ".deliveryMode", displayKey);
                }

                if (!SupportedBundleOverrideModes.Contains(overrideMode))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.UnknownBundleOverrideMode, "Unknown bundleOverrideMode value '" + entry.BundleOverrideMode + "'.", sourcePath + ".bundleOverrideMode", displayKey);
                }

                if (string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceBundle, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(entry.BundleOverrideValue))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.BundleOverrideValueRequired, "forceBundle override requires bundleOverrideValue.", sourcePath + ".bundleOverrideValue", displayKey);
                }

                bool externalOrExcluded =
                    string.Equals(deliveryMode, GlobalResourceBuildProfileDeliveryModes.External, StringComparison.Ordinal) ||
                    string.Equals(deliveryMode, GlobalResourceBuildProfileDeliveryModes.Excluded, StringComparison.Ordinal) ||
                    string.Equals(deliveryMode, GlobalResourceBuildProfileDeliveryModes.EditorOnly, StringComparison.Ordinal) ||
                    string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceExternal, StringComparison.Ordinal) ||
                    string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.Exclude, StringComparison.Ordinal);
                bool forcedInternal =
                    string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceBundle, StringComparison.Ordinal) ||
                    string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceStandalone, StringComparison.Ordinal);
                if (externalOrExcluded && !forcedInternal && !string.IsNullOrWhiteSpace(entry.BundleRule))
                {
                    Add(report, IssueSeverity.Warning, GlobalResourceBuildProfileValidationCodes.ExternalEntryBundleRuleIgnored, "Bundle rule is ignored for external, editor-only or excluded entries.", sourcePath + ".bundleRule", displayKey);
                }

                bool runtimeLoadable = IsRuntimeLoadable(entry);
                if (runtimeLoadable && entry.Source != null && string.Equals(entry.Source.ProviderId, AuthoringResourceProviderIds.UnityAssetDatabase, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(entry.Source.UnityGuid))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.RuntimeUnityGuidRequired, "Runtime-loadable Unity AssetDatabase entry requires unityGuid.", sourcePath + ".source.unityGuid", displayKey);
                }

                if (runtimeLoadable &&
                    string.Equals(deliveryMode, GlobalResourceBuildProfileDeliveryModes.Internal, StringComparison.Ordinal) &&
                    !forcedInternal &&
                    !string.IsNullOrWhiteSpace(entry.BundleRule) &&
                    !rulesById.ContainsKey(entry.BundleRule))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.BundleRuleMissing, "Runtime-loadable internal entry references a missing bundle rule.", sourcePath + ".bundleRule", displayKey);
                }
                else if (runtimeLoadable &&
                    string.Equals(deliveryMode, GlobalResourceBuildProfileDeliveryModes.Internal, StringComparison.Ordinal) &&
                    !forcedInternal &&
                    !EntryMatchesAnyBundleRule(entry, profile, rulesById))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.RuntimeBundleRuleRequired, "Runtime-loadable internal entry must be assigned to a defined bundle rule or use forceStandalone.", sourcePath + ".bundleRule", displayKey);
                }
            }

            ValidateBundleRules(report, profile);
            ValidatePreloadGroups(report, profile);
            ValidateRequiredDomainPlanKeys(report, profile, keys);

            return report;
        }

        private static Dictionary<string, GlobalResourceBuildProfileBundleRule> BuildRulesById(GlobalResourceBuildProfile profile)
        {
            var result = new Dictionary<string, GlobalResourceBuildProfileBundleRule>(StringComparer.Ordinal);
            if (profile == null || profile.BundleRules == null)
                return result;

            for (int i = 0; i < profile.BundleRules.Count; i++)
            {
                GlobalResourceBuildProfileBundleRule rule = profile.BundleRules[i];
                if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || result.ContainsKey(rule.Id))
                    continue;

                result.Add(rule.Id, rule);
            }

            return result;
        }

        private static bool EntryMatchesAnyBundleRule(GlobalResourceBuildProfileEntry entry, GlobalResourceBuildProfile profile, Dictionary<string, GlobalResourceBuildProfileBundleRule> rulesById)
        {
            if (entry == null)
                return false;

            if (!string.IsNullOrWhiteSpace(entry.BundleRule) && rulesById.ContainsKey(entry.BundleRule))
                return true;

            foreach (GlobalResourceBuildProfileBundleRule rule in rulesById.Values)
            {
                if (RuleMatchesEntry(rule, entry, profile.PackageId))
                    return true;
            }

            return false;
        }

        private static void ValidateBundleRules(GlobalResourceBuildProfileValidationReport report, GlobalResourceBuildProfile profile)
        {
            for (int i = 0; i < profile.BundleRules.Count; i++)
            {
                GlobalResourceBuildProfileBundleRule rule = profile.BundleRules[i];
                if (rule == null)
                    continue;

                string sourcePath = "bundleRules[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                if (!string.IsNullOrWhiteSpace(rule.Compression) && !SupportedCompressions.Contains(rule.Compression))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.UnknownCompression, "Unknown bundle compression value '" + rule.Compression + "'.", sourcePath + ".compression", string.Empty);
                }

                if (!rule.AllowEmpty && !BundleRuleSelectsAnyEntry(rule, profile))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.BundleRuleEmpty, "Bundle rule selects no resources.", sourcePath, string.Empty);
                }
            }
        }

        private static void ValidatePreloadGroups(GlobalResourceBuildProfileValidationReport report, GlobalResourceBuildProfile profile)
        {
            for (int i = 0; i < profile.PreloadGroups.Count; i++)
            {
                GlobalResourceBuildProfilePreloadGroup group = profile.PreloadGroups[i];
                if (group == null)
                    continue;

                bool empty = IsNullOrEmpty(group.Labels) && IsNullOrEmpty(group.ExplicitKeys);
                if (empty && !group.AllowEmpty)
                {
                    string sourcePath = "preloadGroups[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.PreloadGroupEmpty, "Preload group has no labels or explicit keys.", sourcePath, string.Empty);
                }
            }
        }

        private static void ValidateRequiredDomainPlanKeys(GlobalResourceBuildProfileValidationReport report, GlobalResourceBuildProfile profile, HashSet<string> profileKeys)
        {
            for (int i = 0; i < profile.RequiredDomainPlanKeys.Count; i++)
            {
                GlobalResourceBuildProfileResourceKey key = profile.RequiredDomainPlanKeys[i];
                string canonicalKey = CanonicalKey(key, profile.PackageId);
                if (!string.IsNullOrEmpty(canonicalKey) && !profileKeys.Contains(canonicalKey))
                {
                    Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.RequiredDomainPlanKeyMissing, "Runtime-required domain plan key is missing from the global profile.", "requiredDomainPlanKeys[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]", FormatKey(key, profile.PackageId));
                }
            }
        }

        private static void ValidateKey(GlobalResourceBuildProfileValidationReport report, GlobalResourceBuildProfileResourceKey key, string sourcePath, string displayKey)
        {
            if (key == null || string.IsNullOrWhiteSpace(key.Id))
            {
                Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.ResourceKeyIdRequired, "ResourceKey id is required.", sourcePath + ".id", displayKey);
            }
            else if (!HasValidKeyCharacters(key.Id))
            {
                Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.ResourceKeyInvalidCharacters, "ResourceKey id contains invalid characters.", sourcePath + ".id", displayKey);
            }

            if (key == null || string.IsNullOrWhiteSpace(EffectiveType(key)))
            {
                Add(report, IssueSeverity.Error, GlobalResourceBuildProfileValidationCodes.ResourceKeyTypeRequired, "ResourceKey type or typeId is required.", sourcePath + ".type", displayKey);
            }
        }

        private static bool BundleRuleSelectsAnyEntry(GlobalResourceBuildProfileBundleRule rule, GlobalResourceBuildProfile profile)
        {
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                GlobalResourceBuildProfileEntry entry = profile.Entries[i];
                if (entry == null)
                    continue;

                if (!UsesDeclaredBundleRules(entry))
                    continue;

                if (RuleMatchesEntry(rule, entry, profile.PackageId))
                    return true;
            }

            return false;
        }

        private static bool RuleMatchesEntry(GlobalResourceBuildProfileBundleRule rule, GlobalResourceBuildProfileEntry entry, string defaultPackageId)
        {
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

            for (int keyIndex = 0; keyIndex < rule.ExplicitKeys.Count; keyIndex++)
            {
                if (string.Equals(entryKey, CanonicalKey(rule.ExplicitKeys[keyIndex], defaultPackageId), StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool UsesDeclaredBundleRules(GlobalResourceBuildProfileEntry entry)
        {
            if (!IsRuntimeLoadable(entry))
                return false;

            string overrideMode = EffectiveBundleOverrideMode(entry);
            return !string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceBundle, StringComparison.Ordinal) &&
                !string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceStandalone, StringComparison.Ordinal);
        }

        private static string CanonicalKey(GlobalResourceBuildProfileResourceKey key, string defaultPackageId)
        {
            if (key == null || string.IsNullOrWhiteSpace(key.Id) || string.IsNullOrWhiteSpace(EffectiveType(key)))
                return string.Empty;

            return EffectivePackageId(key, defaultPackageId) + "|" + EffectiveType(key) + "|" + key.Id + "|" + (key.Variant ?? string.Empty);
        }

        private static string FormatKey(GlobalResourceBuildProfileResourceKey key, string defaultPackageId)
        {
            if (key == null)
                return string.Empty;

            return EffectivePackageId(key, defaultPackageId) + ":" + EffectiveType(key) + ":" + (key.Id ?? string.Empty) + ":" + (key.Variant ?? string.Empty);
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

        private static string EffectiveBundleOverrideMode(GlobalResourceBuildProfileEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.BundleOverrideMode))
                return GlobalResourceBuildProfileBundleOverrideModes.None;

            return entry.BundleOverrideMode;
        }

        private static bool IsRuntimeLoadable(GlobalResourceBuildProfileEntry entry)
        {
            if (entry == null || !entry.RuntimeLoadable || entry.EditorOnly)
                return false;

            string deliveryMode = EffectiveDeliveryMode(entry);
            string overrideMode = EffectiveBundleOverrideMode(entry);
            return string.Equals(deliveryMode, GlobalResourceBuildProfileDeliveryModes.Internal, StringComparison.Ordinal) &&
                !string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.ForceExternal, StringComparison.Ordinal) &&
                !string.Equals(overrideMode, GlobalResourceBuildProfileBundleOverrideModes.Exclude, StringComparison.Ordinal);
        }

        private static bool HasValidKeyCharacters(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
                    continue;

                return false;
            }

            return true;
        }

        private static bool ContainsAny(List<string> left, List<string> right)
        {
            if (IsNullOrEmpty(left) || IsNullOrEmpty(right))
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (Contains(right, left[i]))
                    return true;
            }

            return false;
        }

        private static bool Contains(List<string> values, string expected)
        {
            if (values == null || string.IsNullOrWhiteSpace(expected))
                return false;

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static List<string> PrefixValues(string prefix, List<string> values)
        {
            var result = new List<string>();
            if (values == null)
                return result;

            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    result.Add(prefix + values[i]);
            }

            return result;
        }

        private static bool IsNullOrEmpty<T>(List<T> values)
        {
            return values == null || values.Count == 0;
        }

        private static void Add(GlobalResourceBuildProfileValidationReport report, IssueSeverity severity, string code, string message, string sourcePath, string resourceKey)
        {
            report.Issues.Add(new GlobalResourceBuildProfileValidationIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                SourcePath = sourcePath ?? string.Empty,
                ResourceKey = resourceKey ?? string.Empty
            });
        }
    }
}
