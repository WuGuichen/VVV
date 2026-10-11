using System;
using System.Collections.Generic;

namespace MxFramework.ResourceMapping.Authoring
{
    public sealed class GlobalResourceBuildPlanCompiler
    {
        private static readonly HashSet<string> ForbiddenProviderDataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "address", "providerId", "resourceKey", "resourceId", "typeId", "variant", "packageId",
            "deliveryIntent", "deliveryMode", "runtimeLoadable", "editorOnly", "forceExternal", "exclude",
            "bundleIntent", "bundleRuleId", "bundleOverrideMode", "bundleOverrideValue", "forceBundle", "forceStandalone"
        };

        /// <remarks>AllocByDesign. Compilation is a pure, low-frequency authoring/build operation.</remarks>
        public GlobalResourceBuildPlanCompilation Compile(ResourceMappingProfileV2 profile, string buildTarget)
        {
            ResourceMappingValidationReport baseValidation = ResourceMappingProfileValidator.Validate(profile);
            ResourceMappingProfileV2 normalized = NormalizeProfile(profile ?? new ResourceMappingProfileV2());
            var plan = new GlobalResourceBuildPlan
            {
                ProfileId = normalized.ProfileId,
                CatalogId = normalized.CatalogId,
                BuildTarget = buildTarget ?? string.Empty,
                MappingInputFingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(ResourceMappingCanonicalJson.Serialize(normalized))
            };

            plan.Diagnostics.AddRange(baseValidation.Diagnostics);
            ValidateCompilerContract(normalized, plan.BuildTarget, plan.Diagnostics);
            if (!HasErrors(plan.Diagnostics))
                BuildPlan(normalized, plan);

            SortDiagnostics(plan.Diagnostics);
            plan.Success = !HasErrors(plan.Diagnostics);
            plan.BuildPlanFingerprint = string.Empty;
            string fingerprintPayload = ResourceMappingCanonicalJson.Serialize(plan);
            plan.BuildPlanFingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(fingerprintPayload);
            return new GlobalResourceBuildPlanCompilation
            {
                Plan = plan,
                CanonicalPlanJson = ResourceMappingCanonicalJson.Serialize(plan)
            };
        }

        /// <remarks>AllocByDesign. Produces the exact normalized Profile bytes used for mapping-input revision checks.</remarks>
        public string SerializeCanonicalProfile(ResourceMappingProfileV2 profile)
        {
            return ResourceMappingCanonicalJson.Serialize(NormalizeProfile(profile ?? new ResourceMappingProfileV2()));
        }

        /// <remarks>AllocByDesign. Computes the optimistic revision over the normalized Profile input.</remarks>
        public string ComputeProfileRevision(ResourceMappingProfileV2 profile)
        {
            return ResourceMappingCanonicalJson.ComputeFingerprint(SerializeCanonicalProfile(profile));
        }

        private static void ValidateCompilerContract(ResourceMappingProfileV2 profile, string concreteBuildTarget, List<ResourceMappingDiagnostic> diagnostics)
        {
            if (!string.Equals(profile.Schema, ResourceMappingSchemas.ProfileV2, StringComparison.Ordinal))
                return;

            var runtimeKeys = new Dictionary<string, ResourceMappingEntry>(StringComparer.Ordinal);
            var preloadIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < profile.PreloadGroups.Count; i++)
            {
                if (profile.PreloadGroups[i] != null)
                    preloadIds.Add(profile.PreloadGroups[i].Id ?? string.Empty);
            }

            for (int i = 0; i < profile.Entries.Count; i++)
            {
                ResourceMappingEntry entry = profile.Entries[i];
                string path = "entries[" + i + "]";
                if (entry == null)
                    continue;
                ValidateProviderData(entry.ProviderData, path + ".providerData", diagnostics);
                if (entry.SourceLocator != null)
                    ValidateProviderData(entry.SourceLocator.ProviderData, path + ".sourceLocator.providerData", diagnostics);

                bool runtime = entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal || entry.DeliveryIntent == ResourceMappingDeliveryIntent.External;
                if (!runtime)
                {
                    if ((entry.Dependencies != null && entry.Dependencies.Count > 0) ||
                        (entry.PreloadGroups != null && entry.PreloadGroups.Count > 0))
                        Error(diagnostics, "NonRuntimeReferencesForbidden", "Non-runtime entries cannot declare dependencies or preload groups.", path);
                    continue;
                }

                if (entry.ResourceKey != null)
                    runtimeKeys[entry.ResourceKey.ToIdentityString()] = entry;
                ValidateStringSet(entry.Labels, path + ".labels", diagnostics);
                ValidateStringSet(entry.PreloadGroups, path + ".preloadGroups", diagnostics);
                IList<string> entryPreloadGroups = entry.PreloadGroups ?? new List<string>();
                for (int groupIndex = 0; groupIndex < entryPreloadGroups.Count; groupIndex++)
                {
                    if (!preloadIds.Contains(entryPreloadGroups[groupIndex] ?? string.Empty))
                        Error(diagnostics, "PreloadGroupUnknown", "Entry references an unknown preload group.", path + ".preloadGroups[" + groupIndex + "]");
                }
            }

            for (int i = 0; i < profile.Entries.Count; i++)
            {
                ResourceMappingEntry entry = profile.Entries[i];
                if (entry == null || (entry.DeliveryIntent != ResourceMappingDeliveryIntent.Internal && entry.DeliveryIntent != ResourceMappingDeliveryIntent.External))
                    continue;
                ValidateDependencies(entry.Dependencies, runtimeKeys, "entries[" + i + "].dependencies", diagnostics);
            }

            for (int i = 0; i < profile.BundleRules.Count; i++)
            {
                ResourceMappingBundleRule rule = profile.BundleRules[i];
                if (rule == null)
                    continue;
                ValidateProviderData(rule.ProviderData, "bundleRules[" + i + "].providerData", diagnostics);
                string resolvedTarget = ResolveBuildTarget(rule.BuildTarget, concreteBuildTarget);
                if (!string.Equals(resolvedTarget, concreteBuildTarget, StringComparison.Ordinal))
                    Error(diagnostics, "BundleBuildTargetMismatch", "Bundle buildTarget must be ActiveBuildTarget or match the requested concrete build target.", "bundleRules[" + i + "].buildTarget");
            }

            string compression = string.Empty;
            for (int i = 0; i < profile.BundleRules.Count; i++)
            {
                ResourceMappingBundleRule rule = profile.BundleRules[i];
                if (rule == null)
                    continue;
                string current = (rule.Compression ?? string.Empty).ToLowerInvariant();
                if (compression.Length == 0) compression = current;
                else if (!string.Equals(compression, current, StringComparison.Ordinal))
                    Error(diagnostics, "MixedBundleCompressionUnsupported", "Unity applies compression per AssetBundle build invocation; production rules must use one compression value.", "bundleRules[" + i + "].compression");
            }

            for (int i = 0; i < profile.PreloadGroups.Count; i++)
            {
                ResourceMappingPreloadGroup group = profile.PreloadGroups[i];
                if (group == null)
                    continue;
                ValidateProviderData(group.ProviderData, "preloadGroups[" + i + "].providerData", diagnostics);
                ValidateStringSet(group.Labels, "preloadGroups[" + i + "].labels", diagnostics);
                ValidateDependencies(group.ExplicitKeys, runtimeKeys, "preloadGroups[" + i + "].explicitKeys", diagnostics);
            }
        }

        private static void BuildPlan(ResourceMappingProfileV2 profile, GlobalResourceBuildPlan plan)
        {
            var rules = new Dictionary<string, ResourceMappingBundleRule>(StringComparer.Ordinal);
            var bundles = new Dictionary<string, GlobalResourceBuildPlanBundle>(StringComparer.Ordinal);
            for (int i = 0; i < profile.BundleRules.Count; i++)
            {
                ResourceMappingBundleRule rule = profile.BundleRules[i];
                rules.Add(rule.Id, rule);
                bundles.Add(rule.Id, new GlobalResourceBuildPlanBundle
                {
                    BundleId = rule.Id,
                    BundleName = rule.BundleName,
                    Compression = rule.Compression,
                    BuildTarget = ResolveBuildTarget(rule.BuildTarget, plan.BuildTarget),
                    IncludeDependencies = rule.IncludeDependencies
                });
            }

            var mounts = new Dictionary<string, GlobalResourceBuildPlanCatalogMount>(StringComparer.Ordinal);
            for (int i = 0; i < profile.CatalogMounts.Count; i++)
            {
                ResourceMappingCatalogMount source = profile.CatalogMounts[i];
                var mount = new GlobalResourceBuildPlanCatalogMount
                {
                    CatalogMountId = source.CatalogMountId,
                    CatalogId = source.CatalogId,
                    BootstrapKind = source.BootstrapKind,
                    RelativeCatalogPath = source.RelativeCatalogPath,
                    MountOrder = source.MountOrder,
                    ExpectedCatalogFingerprint = source.ExpectedCatalogFingerprint
                };
                plan.CatalogMounts.Add(mount);
                mounts.Add(mount.CatalogMountId, mount);
            }

            for (int i = 0; i < profile.Entries.Count; i++)
            {
                ResourceMappingEntry source = profile.Entries[i];
                IncrementCount(plan.Counts, source.DeliveryIntent);
                if (source.DeliveryIntent == ResourceMappingDeliveryIntent.EditorOnly || source.DeliveryIntent == ResourceMappingDeliveryIntent.Excluded)
                    continue;

                var entry = new GlobalResourceBuildPlanEntry
                {
                    ProducerReceipt = CloneReceipt(source.ProducerReceipt),
                    AuthoringRecordId = source.AuthoringRecordId,
                    ResourceKey = CloneKey(source.ResourceKey),
                    SourceLocator = CloneSource(source.SourceLocator),
                    DeliveryIntent = source.DeliveryIntent,
                    OverrideIntent = source.OverrideIntent,
                    Labels = NormalizeStrings(source.Labels),
                    PreloadGroups = NormalizeStrings(source.PreloadGroups),
                    Dependencies = NormalizeKeys(source.Dependencies),
                    ProviderData = CloneDictionary(source.ProviderData)
                };

                if (source.DeliveryIntent == ResourceMappingDeliveryIntent.Internal)
                {
                    entry.BundleRuleId = source.BundleIntent.RuleId;
                    entry.BundleId = source.BundleIntent.RuleId;
                    entry.EffectiveMountOrder = -1;
                    bundles[entry.BundleRuleId].ResourceKeys.Add(CloneKey(entry.ResourceKey));
                }
                else
                {
                    entry.CatalogMountId = source.ExternalCatalogBinding.CatalogMountId;
                    entry.EffectiveMountOrder = mounts[entry.CatalogMountId].MountOrder;
                    mounts[entry.CatalogMountId].AllowlistedKeys.Add(new GlobalResourceBuildPlanCatalogAllowlistedKey
                    {
                        ResourceKey = CloneKey(entry.ResourceKey),
                        OverrideIntent = entry.OverrideIntent.GetValueOrDefault(ResourceMappingOverrideIntent.Forbid)
                    });
                }
                plan.Entries.Add(entry);
            }

            foreach (KeyValuePair<string, GlobalResourceBuildPlanBundle> pair in bundles)
            {
                pair.Value.ResourceKeys = NormalizeKeys(pair.Value.ResourceKeys);
                ResourceMappingBundleRule rule = rules[pair.Key];
                if (pair.Value.ResourceKeys.Count == 0 && !rule.AllowEmpty)
                    Error(plan.Diagnostics, "BundleRuleEmpty", "Bundle rule has no owned internal resources: " + rule.Id + ".", "bundleRules." + rule.Id);
                plan.Bundles.Add(pair.Value);
            }

            for (int i = 0; i < plan.CatalogMounts.Count; i++)
            {
                plan.CatalogMounts[i].AllowlistedKeys.Sort((left, right) =>
                    string.Compare(left.ResourceKey.ToIdentityString(), right.ResourceKey.ToIdentityString(), StringComparison.Ordinal));
                if (plan.CatalogMounts[i].AllowlistedKeys.Count == 0)
                    Warning(plan.Diagnostics, "CatalogMountEmpty", "Catalog mount has no allowlisted external resources.", "catalogMounts." + plan.CatalogMounts[i].CatalogMountId);
            }

            BuildPreloadGroups(profile, plan);
            ValidateRuntimeRouteOrder(plan);
            plan.Entries.Sort(ComparePlanEntries);
            plan.Bundles.Sort((left, right) => string.Compare(left.BundleId, right.BundleId, StringComparison.Ordinal));
            plan.CatalogMounts.Sort(CompareMounts);
            plan.PreloadGroups.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
        }

        private static void BuildPreloadGroups(ResourceMappingProfileV2 profile, GlobalResourceBuildPlan plan)
        {
            var groups = new Dictionary<string, GlobalResourceBuildPlanPreloadGroup>(StringComparer.Ordinal);
            for (int i = 0; i < profile.PreloadGroups.Count; i++)
            {
                ResourceMappingPreloadGroup source = profile.PreloadGroups[i];
                groups.Add(source.Id, new GlobalResourceBuildPlanPreloadGroup
                {
                    Id = source.Id,
                    FailFast = source.FailFast,
                    MaxConcurrentLoads = source.MaxConcurrentLoads,
                    ResourceKeys = NormalizeKeys(source.ExplicitKeys)
                });
            }

            for (int i = 0; i < plan.Entries.Count; i++)
            {
                GlobalResourceBuildPlanEntry entry = plan.Entries[i];
                for (int groupIndex = 0; groupIndex < entry.PreloadGroups.Count; groupIndex++)
                    groups[entry.PreloadGroups[groupIndex]].ResourceKeys.Add(CloneKey(entry.ResourceKey));
            }

            for (int i = 0; i < profile.PreloadGroups.Count; i++)
            {
                ResourceMappingPreloadGroup source = profile.PreloadGroups[i];
                GlobalResourceBuildPlanPreloadGroup group = groups[source.Id];
                if (source.Labels != null && source.Labels.Count > 0)
                {
                    var labelSet = new HashSet<string>(source.Labels, StringComparer.Ordinal);
                    for (int entryIndex = 0; entryIndex < plan.Entries.Count; entryIndex++)
                    {
                        GlobalResourceBuildPlanEntry entry = plan.Entries[entryIndex];
                        for (int labelIndex = 0; labelIndex < entry.Labels.Count; labelIndex++)
                        {
                            if (labelSet.Contains(entry.Labels[labelIndex]))
                            {
                                group.ResourceKeys.Add(CloneKey(entry.ResourceKey));
                                break;
                            }
                        }
                    }
                }
                group.ResourceKeys = NormalizeKeys(group.ResourceKeys);
                if (group.ResourceKeys.Count == 0 && !source.AllowEmpty)
                    Error(plan.Diagnostics, "PreloadGroupEmpty", "Preload group resolved no runtime resources.", "preloadGroups." + source.Id);
                plan.PreloadGroups.Add(group);
            }
        }

        private static void ValidateRuntimeRouteOrder(GlobalResourceBuildPlan plan)
        {
            var ordered = new List<GlobalResourceBuildPlanEntry>(plan.Entries);
            ordered.Sort(CompareApplicationOrder);
            var global = new Dictionary<string, GlobalResourceBuildPlanEntry>(StringComparer.Ordinal);
            var idVariants = new Dictionary<string, GlobalResourceBuildPlanEntry>(StringComparer.Ordinal);
            for (int i = 0; i < ordered.Count; i++)
            {
                GlobalResourceBuildPlanEntry entry = ordered[i];
                string route = WithoutPackageIdentity(entry.ResourceKey);
                string idVariant = (entry.ResourceKey.Id ?? string.Empty) + "|" + (entry.ResourceKey.Variant ?? string.Empty);
                if (global.TryGetValue(route, out GlobalResourceBuildPlanEntry previous))
                {
                    if (string.Equals(SourceCatalogIdentity(previous), SourceCatalogIdentity(entry), StringComparison.Ordinal))
                        Error(plan.Diagnostics, "CatalogLocalGlobalRouteDuplicate", "Entries from the same source Catalog cannot share one package-less runtime route.", EntryPath(entry));
                    else if (entry.OverrideIntent != ResourceMappingOverrideIntent.Allow)
                        Error(plan.Diagnostics, "GlobalRouteOverrideRequired", "Later global route collision requires overrideIntent=allow.", EntryPath(entry));
                    else if (!string.Equals(previous.ResourceKey.TypeId, entry.ResourceKey.TypeId, StringComparison.Ordinal))
                        Error(plan.Diagnostics, "GlobalRouteTypeMismatch", "Global route override must preserve TypeId.", EntryPath(entry));
                }
                else if (entry.OverrideIntent == ResourceMappingOverrideIntent.Allow &&
                         idVariants.TryGetValue(idVariant, out previous) &&
                         !string.Equals(previous.ResourceKey.TypeId, entry.ResourceKey.TypeId, StringComparison.Ordinal))
                {
                    Error(plan.Diagnostics, "GlobalRouteTypeMismatch", "Override conflicts with an existing id/variant of another TypeId.", EntryPath(entry));
                }
                global[route] = entry;
                idVariants[idVariant] = entry;
            }
        }

        private static string SourceCatalogIdentity(GlobalResourceBuildPlanEntry entry) =>
            entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal
                ? "primary"
                : "mount:" + (entry.CatalogMountId ?? string.Empty);

        private static ResourceMappingProfileV2 NormalizeProfile(ResourceMappingProfileV2 source)
        {
            var result = new ResourceMappingProfileV2
            {
                Schema = source.Schema,
                ProfileId = source.ProfileId,
                CatalogId = source.CatalogId,
                Metadata = CloneDictionary(source.Metadata)
            };
            if (source.Entries != null)
            {
                for (int i = 0; i < source.Entries.Count; i++)
                    result.Entries.Add(CloneEntry(source.Entries[i]));
            }
            if (source.BundleRules != null)
            {
                for (int i = 0; i < source.BundleRules.Count; i++)
                    result.BundleRules.Add(CloneRule(source.BundleRules[i]));
            }
            if (source.CatalogMounts != null)
            {
                for (int i = 0; i < source.CatalogMounts.Count; i++)
                    result.CatalogMounts.Add(CloneMount(source.CatalogMounts[i]));
            }
            if (source.PreloadGroups != null)
            {
                for (int i = 0; i < source.PreloadGroups.Count; i++)
                    result.PreloadGroups.Add(ClonePreload(source.PreloadGroups[i]));
            }
            result.Entries.Sort(CompareEntries);
            result.BundleRules.Sort((left, right) => string.Compare(left != null ? left.Id : string.Empty, right != null ? right.Id : string.Empty, StringComparison.Ordinal));
            result.CatalogMounts.Sort((left, right) => CompareMountSources(left, right));
            result.PreloadGroups.Sort((left, right) => string.Compare(left != null ? left.Id : string.Empty, right != null ? right.Id : string.Empty, StringComparison.Ordinal));
            return result;
        }

        private static ResourceMappingEntry CloneEntry(ResourceMappingEntry source)
        {
            if (source == null)
                return null;
            return new ResourceMappingEntry
            {
                ProducerReceipt = CloneReceipt(source.ProducerReceipt), AuthoringRecordId = source.AuthoringRecordId,
                ResourceKey = CloneKey(source.ResourceKey), SourceLocator = CloneSource(source.SourceLocator),
                DeliveryIntent = source.DeliveryIntent, OverrideIntent = source.OverrideIntent,
                ExternalCatalogBinding = source.ExternalCatalogBinding == null ? null : new ResourceMappingExternalCatalogBinding { CatalogMountId = source.ExternalCatalogBinding.CatalogMountId },
                BundleIntent = source.BundleIntent == null ? null : new ResourceMappingBundleIntent { RuleId = source.BundleIntent.RuleId },
                Labels = NormalizeStrings(source.Labels), PreloadGroups = NormalizeStrings(source.PreloadGroups),
                Dependencies = NormalizeKeys(source.Dependencies), ProviderData = CloneDictionary(source.ProviderData)
            };
        }

        private static ResourceMappingBundleRule CloneRule(ResourceMappingBundleRule source)
        {
            if (source == null)
                return null;
            return new ResourceMappingBundleRule
            {
                ProducerReceipt = CloneReceipt(source.ProducerReceipt), AuthoringRecordId = source.AuthoringRecordId,
                Id = source.Id, BundleName = source.BundleName, Compression = source.Compression, BuildTarget = source.BuildTarget,
                IncludeDependencies = source.IncludeDependencies, AllowEmpty = source.AllowEmpty, ProviderData = CloneDictionary(source.ProviderData)
            };
        }

        private static ResourceMappingCatalogMount CloneMount(ResourceMappingCatalogMount source)
        {
            if (source == null)
                return null;
            return new ResourceMappingCatalogMount
            {
                ProducerReceipt = CloneReceipt(source.ProducerReceipt), AuthoringRecordId = source.AuthoringRecordId,
                CatalogMountId = source.CatalogMountId, CatalogId = source.CatalogId, BootstrapKind = source.BootstrapKind,
                RelativeCatalogPath = source.RelativeCatalogPath, MountOrder = source.MountOrder,
                ExpectedCatalogFingerprint = source.ExpectedCatalogFingerprint
            };
        }

        private static ResourceMappingPreloadGroup ClonePreload(ResourceMappingPreloadGroup source)
        {
            if (source == null)
                return null;
            return new ResourceMappingPreloadGroup
            {
                ProducerReceipt = CloneReceipt(source.ProducerReceipt), AuthoringRecordId = source.AuthoringRecordId,
                Id = source.Id, ExplicitKeys = NormalizeKeys(source.ExplicitKeys), Labels = NormalizeStrings(source.Labels),
                FailFast = source.FailFast, MaxConcurrentLoads = source.MaxConcurrentLoads, AllowEmpty = source.AllowEmpty,
                ProviderData = CloneDictionary(source.ProviderData)
            };
        }

        private static ResourceMappingProducerReceipt CloneReceipt(ResourceMappingProducerReceipt source)
        {
            return source == null ? null : new ResourceMappingProducerReceipt
            {
                ProducerId = source.ProducerId,
                ProducerContractVersion = source.ProducerContractVersion,
                SourceRecordFingerprint = source.SourceRecordFingerprint
            };
        }

        private static ResourceMappingResourceKey CloneKey(ResourceMappingResourceKey source)
        {
            return source == null ? null : new ResourceMappingResourceKey
            {
                Id = source.Id, TypeId = source.TypeId, Variant = source.Variant, PackageId = source.PackageId
            };
        }

        private static ResourceMappingSourceLocator CloneSource(ResourceMappingSourceLocator source)
        {
            return source == null ? null : new ResourceMappingSourceLocator
            {
                Kind = source.Kind, UnityGuid = source.UnityGuid, CachedPath = source.CachedPath,
                GeneratedAssetId = source.GeneratedAssetId, DomainSourceId = source.DomainSourceId,
                PackageRelativePath = source.PackageRelativePath, ContentHash = source.ContentHash,
                ProviderData = CloneDictionary(source.ProviderData)
            };
        }

        private static Dictionary<string, string> CloneDictionary(IDictionary<string, string> source)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (source == null)
                return result;
            var keys = new List<string>(source.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
                result[keys[i]] = source[keys[i]];
            return result;
        }

        private static List<string> NormalizeStrings(IList<string> values)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                    set.Add(values[i] ?? string.Empty);
            }
            var result = new List<string>(set);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static List<ResourceMappingResourceKey> NormalizeKeys(IList<ResourceMappingResourceKey> values)
        {
            var byIdentity = new Dictionary<string, ResourceMappingResourceKey>(StringComparer.Ordinal);
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    ResourceMappingResourceKey key = values[i];
                    string identity = key != null ? key.ToIdentityString() : string.Empty;
                    if (!byIdentity.ContainsKey(identity))
                        byIdentity.Add(identity, CloneKey(key));
                }
            }
            var result = new List<ResourceMappingResourceKey>(byIdentity.Values);
            result.Sort(CompareKeys);
            return result;
        }

        private static void ValidateDependencies(
            IList<ResourceMappingResourceKey> dependencies,
            IDictionary<string, ResourceMappingEntry> runtimeKeys,
            string path,
            List<ResourceMappingDiagnostic> diagnostics)
        {
            if (dependencies == null)
                return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < dependencies.Count; i++)
            {
                ResourceMappingResourceKey key = dependencies[i];
                string identity = key != null ? key.ToIdentityString() : string.Empty;
                if (key == null || string.IsNullOrWhiteSpace(key.Id) || string.IsNullOrWhiteSpace(key.TypeId))
                    Error(diagnostics, "DependencyKeyInvalid", "Dependency must be one complete ResourceKey.", path + "[" + i + "]");
                else if (!seen.Add(identity))
                    Warning(diagnostics, "DependencyDuplicate", "Duplicate dependency is canonicalized once.", path + "[" + i + "]");
                else if (!runtimeKeys.ContainsKey(identity))
                    Error(diagnostics, "DependencyUnknown", "Dependency does not resolve one runtime Profile entry: " + identity + ".", path + "[" + i + "]");
            }
        }

        private static void ValidateProviderData(IDictionary<string, string> data, string path, List<ResourceMappingDiagnostic> diagnostics)
        {
            if (data == null)
                return;
            foreach (KeyValuePair<string, string> pair in data)
            {
                if (ForbiddenProviderDataKeys.Contains(pair.Key ?? string.Empty))
                    Error(diagnostics, "ProviderDataAuthorityForbidden", "ProviderData cannot carry mapping or runtime routing authority: " + pair.Key + ".", path + "." + pair.Key);
            }
        }

        private static void ValidateStringSet(IList<string> values, string path, List<ResourceMappingDiagnostic> diagnostics)
        {
            if (values == null)
                return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                    Error(diagnostics, "StringSetValueInvalid", "Set value must be non-empty and trimmed.", path + "[" + i + "]");
                else if (!seen.Add(value))
                    Warning(diagnostics, "StringSetDuplicate", "Duplicate set value is canonicalized once.", path + "[" + i + "]");
            }
        }

        private static string ResolveBuildTarget(string ruleTarget, string requestedTarget)
        {
            return string.Equals(ruleTarget, "ActiveBuildTarget", StringComparison.Ordinal) ? requestedTarget : (ruleTarget ?? string.Empty);
        }

        private static void IncrementCount(GlobalResourceBuildPlanCounts counts, ResourceMappingDeliveryIntent delivery)
        {
            if (delivery == ResourceMappingDeliveryIntent.Internal) counts.Internal++;
            else if (delivery == ResourceMappingDeliveryIntent.External) counts.External++;
            else if (delivery == ResourceMappingDeliveryIntent.EditorOnly) counts.EditorOnly++;
            else counts.Excluded++;
        }

        private static int CompareEntries(ResourceMappingEntry left, ResourceMappingEntry right)
        {
            bool leftRuntime = left != null && left.ResourceKey != null;
            bool rightRuntime = right != null && right.ResourceKey != null;
            if (leftRuntime && rightRuntime)
                return CompareKeys(left.ResourceKey, right.ResourceKey);
            if (leftRuntime != rightRuntime)
                return leftRuntime ? -1 : 1;
            return string.Compare(EntryPath(left), EntryPath(right), StringComparison.Ordinal);
        }

        private static int ComparePlanEntries(GlobalResourceBuildPlanEntry left, GlobalResourceBuildPlanEntry right)
        {
            return CompareKeys(left.ResourceKey, right.ResourceKey);
        }

        private static int CompareKeys(ResourceMappingResourceKey left, ResourceMappingResourceKey right)
        {
            return string.Compare(left != null ? left.ToIdentityString() : string.Empty, right != null ? right.ToIdentityString() : string.Empty, StringComparison.Ordinal);
        }

        private static int CompareMountSources(ResourceMappingCatalogMount left, ResourceMappingCatalogMount right)
        {
            int order = (left != null ? left.MountOrder : -1).CompareTo(right != null ? right.MountOrder : -1);
            return order != 0 ? order : string.Compare(left != null ? left.CatalogMountId : string.Empty, right != null ? right.CatalogMountId : string.Empty, StringComparison.Ordinal);
        }

        private static int CompareMounts(GlobalResourceBuildPlanCatalogMount left, GlobalResourceBuildPlanCatalogMount right)
        {
            int order = left.MountOrder.CompareTo(right.MountOrder);
            return order != 0 ? order : string.Compare(left.CatalogMountId, right.CatalogMountId, StringComparison.Ordinal);
        }

        private static int CompareApplicationOrder(GlobalResourceBuildPlanEntry left, GlobalResourceBuildPlanEntry right)
        {
            int order = left.EffectiveMountOrder.CompareTo(right.EffectiveMountOrder);
            return order != 0 ? order : ComparePlanEntries(left, right);
        }

        private static string WithoutPackageIdentity(ResourceMappingResourceKey key)
        {
            return (key.Id ?? string.Empty) + "|" + (key.TypeId ?? string.Empty) + "|" + (key.Variant ?? string.Empty);
        }

        private static string EntryPath(ResourceMappingEntry entry)
        {
            return entry == null ? string.Empty : ((entry.ProducerReceipt != null ? entry.ProducerReceipt.ProducerId : string.Empty) + "/" + (entry.AuthoringRecordId ?? string.Empty));
        }

        private static string EntryPath(GlobalResourceBuildPlanEntry entry)
        {
            return "entries." + (entry != null ? entry.AuthoringRecordId : string.Empty);
        }

        private static bool HasErrors(IList<ResourceMappingDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == ResourceMappingDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }

        private static void SortDiagnostics(List<ResourceMappingDiagnostic> diagnostics)
        {
            diagnostics.Sort((left, right) =>
            {
                int severity = right.Severity.CompareTo(left.Severity);
                if (severity != 0) return severity;
                int code = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
                return code != 0 ? code : string.Compare(left.Path, right.Path, StringComparison.Ordinal);
            });
        }

        private static void Error(List<ResourceMappingDiagnostic> diagnostics, string code, string message, string path)
        {
            diagnostics.Add(new ResourceMappingDiagnostic { Severity = ResourceMappingDiagnosticSeverity.Error, Code = code, Message = message, Path = path });
        }

        private static void Warning(List<ResourceMappingDiagnostic> diagnostics, string code, string message, string path)
        {
            diagnostics.Add(new ResourceMappingDiagnostic { Severity = ResourceMappingDiagnosticSeverity.Warning, Code = code, Message = message, Path = path });
        }
    }
}
