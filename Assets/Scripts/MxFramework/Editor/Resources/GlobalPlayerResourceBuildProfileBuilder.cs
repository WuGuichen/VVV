using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class GlobalPlayerResourceBuildProfileBuilder
    {
        public const string ProfilePath = "Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json";
        public const string CatalogPath = "Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json";
        public const string PreloadGroupsPath = "Assets/StreamingAssets/MxFramework/Resources/global_preload_groups.json";
        public const string BundleDependenciesPath = "Assets/StreamingAssets/MxFramework/Resources/global_bundle_dependencies.json";
        public const string BuildReportPath = "Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json";
        public const string BundleRootPath = "Assets/StreamingAssets/MxFramework/Resources/Bundles";

        private const string ValidateMenuPath = "MxFramework/Resources/Validate Global Resource Build Profile";
        private const string BuildMenuPath = "MxFramework/Resources/Build Global Player Resource Catalog";

        [MenuItem(ValidateMenuPath, priority = 130)]
        public static void ValidateGlobalProfile()
        {
            GlobalResourceBuildPlan plan = CreateBuildPlan(ProfilePath);
            if (plan.HasErrors)
                Debug.LogError(CreateReportText(plan.Report));
            else
                Debug.Log("Global resource build profile validated. Entries: " + plan.Entries.Count + ", bundles: " + plan.Bundles.Count + ".");
        }

        [MenuItem(BuildMenuPath, priority = 131)]
        public static void BuildGlobalPlayerResourceCatalog()
        {
            GlobalResourceBuildPlan plan = CreateBuildPlan(ProfilePath);
            if (plan.HasErrors)
            {
                Debug.LogError(CreateReportText(plan.Report));
                return;
            }

            Build(plan);
            Debug.Log("Global player resource catalog generated: " + CatalogPath);
        }

        public static GlobalResourceBuildPlan CreateBuildPlan(string profilePath)
        {
            var report = new GlobalResourceBuildReport
            {
                BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString()
            };

            GlobalResourceBuildProfileDocument profile = LoadProfile(profilePath, report);
            var plan = new GlobalResourceBuildPlan(profile, report);
            if (profile == null)
                return plan;

            report.ProfileId = profile.profileId ?? string.Empty;
            report.CatalogId = profile.catalogId ?? string.Empty;
            report.PackageId = profile.packageId ?? string.Empty;

            ResolveEntries(plan);
            ExpandBundleRules(plan);
            ValidatePlan(plan);
            SortPlan(plan);
            return plan;
        }

        public static void Build(GlobalResourceBuildPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (plan.HasErrors)
                throw new InvalidOperationException("Global resource build plan has validation errors.");

            string buildTargetName = EditorUserBuildSettings.activeBuildTarget.ToString();
            string bundleOutputPath = BundleRootPath + "/" + buildTargetName;
            EnsureAssetFolder(bundleOutputPath);

            string tempOutput = Path.Combine("Temp", "MxFrameworkGlobalResourceBundles", buildTargetName);
            if (Directory.Exists(tempOutput))
                Directory.Delete(tempOutput, true);
            Directory.CreateDirectory(tempOutput);

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                tempOutput,
                CreateAssetBundleBuilds(plan),
                ResolveBuildOptions(plan),
                EditorUserBuildSettings.activeBuildTarget);

            if (manifest == null)
                throw new InvalidOperationException("Unity failed to build global resource AssetBundles.");

            CopyGeneratedBundles(plan, tempOutput, bundleOutputPath);
            ResourceCatalog catalog = CreateCatalog(plan, bundleOutputPath);
            ResourceCatalogValidationReport catalogReport = ResourceCatalogEditorValidator.ValidateCatalog(catalog, new[] { AssetBundleProvider.Id });
            catalogReport.Merge(ValidateBundleFiles(plan, catalog, bundleOutputPath));
            if (catalogReport.HasErrors)
                throw new InvalidOperationException(ResourceCatalogEditorValidator.CreateReportText(catalog, catalogReport));

            EnsureAssetFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
            EnsureAssetFolder(Path.GetDirectoryName(PreloadGroupsPath)?.Replace('\\', '/'));
            EnsureAssetFolder(Path.GetDirectoryName(BundleDependenciesPath)?.Replace('\\', '/'));
            EnsureAssetFolder(Path.GetDirectoryName(BuildReportPath)?.Replace('\\', '/'));

            File.WriteAllText(CatalogPath, SampleResourceCatalogBuilder.WriteCatalogJson(catalog));
            File.WriteAllText(PreloadGroupsPath, WritePreloadGroupsJson(plan));
            File.WriteAllText(BundleDependenciesPath, WriteBundleDependenciesJson(plan, manifest));
            File.WriteAllText(BuildReportPath, WriteBuildReportJson(plan));

            AssetDatabase.ImportAsset(CatalogPath);
            AssetDatabase.ImportAsset(PreloadGroupsPath);
            AssetDatabase.ImportAsset(BundleDependenciesPath);
            AssetDatabase.ImportAsset(BuildReportPath);
            AssetDatabase.Refresh();
        }

        private static GlobalResourceBuildProfileDocument LoadProfile(string profilePath, GlobalResourceBuildReport report)
        {
            if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
            {
                report.AddError("ProfileMissing", "Global resource build profile was not found: " + profilePath + ".", "profile");
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<GlobalResourceBuildProfileDocument>(File.ReadAllText(profilePath));
            }
            catch (JsonException ex)
            {
                report.AddError("ProfileJsonInvalid", "Global resource build profile json could not be parsed: " + ex.Message + ".", "profile");
                return null;
            }
        }

        private static void ResolveEntries(GlobalResourceBuildPlan plan)
        {
            GlobalResourceBuildProfileEntryDocument[] entries = plan.Profile.entries ?? Array.Empty<GlobalResourceBuildProfileEntryDocument>();
            for (int i = 0; i < entries.Length; i++)
            {
                GlobalResourceBuildProfileEntryDocument entry = entries[i];
                if (entry == null)
                    continue;

                var resolved = new GlobalResourceResolvedEntry(entry, "entries[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
                ResolveUnitySource(plan, resolved);
                plan.Entries.Add(resolved);
            }
        }

        private static void ResolveUnitySource(GlobalResourceBuildPlan plan, GlobalResourceResolvedEntry resolved)
        {
            GlobalResourceBuildProfileEntrySourceDocument source = resolved.Entry.source;
            if (source == null || !string.Equals(source.providerId, "unityAssetDatabase", StringComparison.Ordinal))
                return;

            string resolvedPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(source.unityGuid))
            {
                resolvedPath = AssetDatabase.GUIDToAssetPath(source.unityGuid);
                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    plan.Report.AddError("UnityGuidMissing", "Unity asset GUID could not be resolved.", resolved.SourcePath + ".source.unityGuid", resolved.ResourceKeyText);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(source.unityAssetPath) && !string.Equals(source.unityAssetPath, resolvedPath, StringComparison.Ordinal))
                    plan.Report.AddWarning("UnityPathDrift", "Stored Unity asset path differs from GUID resolved path: " + resolvedPath + ".", resolved.SourcePath + ".source.unityAssetPath", resolved.ResourceKeyText);
            }
            else
            {
                resolvedPath = source.unityAssetPath ?? string.Empty;
                if (resolved.RuntimeLoadable)
                    plan.Report.AddError("UnityGuidRequired", "Runtime-loadable Unity source requires unityGuid.", resolved.SourcePath + ".source.unityGuid", resolved.ResourceKeyText);
            }

            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
            {
                plan.Report.AddError("UnityAssetMissing", "Unity source asset was not found: " + resolvedPath + ".", resolved.SourcePath + ".source.unityAssetPath", resolved.ResourceKeyText);
                return;
            }

            Type expectedType = UnityResourceTypeResolver.Resolve(resolved.TypeId);
            Type actualType = AssetDatabase.GetMainAssetTypeAtPath(resolvedPath);
            if (actualType == null)
            {
                plan.Report.AddError("UnityAssetTypeMissing", "Unity source asset type could not be resolved.", resolved.SourcePath + ".resourceKey.type", resolved.ResourceKeyText);
                return;
            }

            if (expectedType != typeof(UnityEngine.Object) && !expectedType.IsAssignableFrom(actualType))
            {
                plan.Report.AddError("UnityAssetTypeMismatch", "Unity source asset type '" + actualType.Name + "' does not match expected '" + expectedType.Name + "'.", resolved.SourcePath + ".resourceKey.type", resolved.ResourceKeyText);
                return;
            }

            resolved.UnityAssetPath = resolvedPath;
            resolved.UnityGuid = string.IsNullOrWhiteSpace(source.unityGuid) ? AssetDatabase.AssetPathToGUID(resolvedPath) : source.unityGuid;
            resolved.UnityMainObjectType = actualType.Name;
        }

        private static void ExpandBundleRules(GlobalResourceBuildPlan plan)
        {
            GlobalResourceBuildProfileBundleRuleDocument[] rules = plan.Profile.bundleRules ?? Array.Empty<GlobalResourceBuildProfileBundleRuleDocument>();
            for (int i = 0; i < rules.Length; i++)
            {
                GlobalResourceBuildProfileBundleRuleDocument rule = rules[i];
                if (rule == null)
                    continue;

                var bundle = new GlobalResourceBuildBundle(rule, "bundleRules[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
                for (int entryIndex = 0; entryIndex < plan.Entries.Count; entryIndex++)
                {
                    GlobalResourceResolvedEntry entry = plan.Entries[entryIndex];
                    if (entry.RuntimeLoadable && ShouldUseDeclaredBundleRules(entry.Entry) && RuleMatchesEntry(rule, entry, plan.Profile.packageId))
                        bundle.Entries.Add(entry);
                }

                plan.Bundles.Add(bundle);
            }

            for (int entryIndex = 0; entryIndex < plan.Entries.Count; entryIndex++)
            {
                GlobalResourceResolvedEntry entry = plan.Entries[entryIndex];
                if (!entry.RuntimeLoadable || !IsForcedInternalBundle(entry.Entry))
                    continue;

                var rule = new GlobalResourceBuildProfileBundleRuleDocument
                {
                    bundleName = GetForcedBundleName(entry.Entry),
                    compression = "lz4",
                    buildTarget = "ActiveBuildTarget",
                    includeDependencies = true,
                    allowEmpty = false
                };
                rule.id = "override." + SanitizeBundleSegment(rule.bundleName);
                GlobalResourceBuildBundle bundle = FindBundleByName(plan.Bundles, rule.bundleName);
                if (bundle == null)
                {
                    bundle = new GlobalResourceBuildBundle(rule, entry.SourcePath + ".bundleOverrideMode");
                    plan.Bundles.Add(bundle);
                }
                bundle.Entries.Add(entry);
            }
        }

        private static GlobalResourceBuildBundle FindBundleByName(List<GlobalResourceBuildBundle> bundles, string bundleName)
        {
            for (int i = 0; i < bundles.Count; i++)
            {
                if (string.Equals(bundles[i].BundleName, bundleName, StringComparison.Ordinal))
                    return bundles[i];
            }

            return null;
        }

        private static void ValidatePlan(GlobalResourceBuildPlan plan)
        {
            var assetToBundle = new Dictionary<string, string>(StringComparer.Ordinal);
            var resourceKeys = new HashSet<string>(StringComparer.Ordinal);
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            string selectedCompression = string.Empty;
            string activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            ValidateEntryKeys(plan, resourceKeys);
            for (int i = 0; i < plan.Bundles.Count; i++)
            {
                GlobalResourceBuildBundle bundle = plan.Bundles[i];
                if (string.IsNullOrWhiteSpace(bundle.Rule.id))
                    plan.Report.AddError("BundleRuleIdMissing", "Bundle rule id is required.", bundle.SourcePath + ".id");
                else
                    ruleIds.Add(bundle.Rule.id);

                if (string.IsNullOrWhiteSpace(bundle.BundleName))
                    plan.Report.AddError("BundleNameMissing", "Bundle rule bundleName is required.", bundle.SourcePath + ".bundleName");
                if (!IsSupportedCompression(bundle.Rule.compression))
                    plan.Report.AddError("BundleCompressionUnknown", "Unknown bundle compression value: " + bundle.Rule.compression + ".", bundle.SourcePath + ".compression");
                else
                    ValidateCompressionConsistency(plan, bundle, ref selectedCompression);
                ValidateBuildTarget(plan, bundle, activeBuildTarget);
                if (!bundle.Rule.includeDependencies)
                    plan.Report.AddWarning("BundleDependenciesDisabled", "includeDependencies is false; generated dependency manifest will not load dependencies for this bundle.", bundle.SourcePath + ".includeDependencies");
                if (!bundle.Rule.allowEmpty && bundle.Entries.Count == 0)
                    plan.Report.AddError("BundleRuleEmpty", "Bundle rule selects no runtime-loadable assets.", bundle.SourcePath);

                for (int entryIndex = 0; entryIndex < bundle.Entries.Count; entryIndex++)
                {
                    GlobalResourceResolvedEntry entry = bundle.Entries[entryIndex];
                    if (string.IsNullOrWhiteSpace(entry.UnityAssetPath))
                        continue;

                    if (assetToBundle.TryGetValue(entry.UnityAssetPath, out string existingBundle) && !string.Equals(existingBundle, bundle.BundleName, StringComparison.Ordinal))
                    {
                        plan.Report.AddError("AssetSelectedByMultipleBundles", "Asset is selected by multiple bundle rules: " + existingBundle + " and " + bundle.BundleName + ".", entry.SourcePath, entry.ResourceKeyText);
                    }
                    else
                    {
                        assetToBundle[entry.UnityAssetPath] = bundle.BundleName;
                    }
                }
            }

            for (int i = 0; i < plan.Entries.Count; i++)
            {
                GlobalResourceResolvedEntry entry = plan.Entries[i];
                if (entry.RuntimeLoadable && ShouldUseDeclaredBundleRules(entry.Entry) && !string.IsNullOrWhiteSpace(entry.Entry.bundleRule) && !ruleIds.Contains(entry.Entry.bundleRule))
                    plan.Report.AddError("BundleRuleMissing", "Runtime-loadable entry references a missing bundle rule.", entry.SourcePath + ".bundleRule", entry.ResourceKeyText);
                if (entry.RuntimeLoadable && ShouldUseDeclaredBundleRules(entry.Entry) && string.IsNullOrWhiteSpace(entry.Entry.bundleRule) && !MatchesAnyBundleRule(plan, entry))
                    plan.Report.AddError("BundleRuleRequired", "Runtime-loadable internal entry must be assigned to a defined bundle rule.", entry.SourcePath + ".bundleRule", entry.ResourceKeyText);
            }

            ReportStaleBundles(plan, activeBuildTarget);
        }

        private static void ValidateEntryKeys(GlobalResourceBuildPlan plan, HashSet<string> resourceKeys)
        {
            for (int i = 0; i < plan.Entries.Count; i++)
            {
                GlobalResourceResolvedEntry entry = plan.Entries[i];
                if (entry == null)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.ResourceKey.id))
                    plan.Report.AddError("ResourceKeyIdRequired", "ResourceKey id is required.", entry.SourcePath + ".resourceKey.id", entry.ResourceKeyText);
                if (string.IsNullOrWhiteSpace(entry.TypeId))
                    plan.Report.AddError("ResourceKeyTypeRequired", "ResourceKey type is required.", entry.SourcePath + ".resourceKey.type", entry.ResourceKeyText);
                if (!HasValidResourceKeyId(entry.ResourceKey.id))
                    plan.Report.AddError("ResourceKeyInvalidCharacters", "ResourceKey id contains invalid characters.", entry.SourcePath + ".resourceKey.id", entry.ResourceKeyText);

                string key = CanonicalKey(entry.ResourceKey, plan.Profile.packageId);
                if (!string.IsNullOrWhiteSpace(key) && !resourceKeys.Add(key))
                    plan.Report.AddError("DuplicateResourceKey", "Duplicate ResourceKey in global resource build profile.", entry.SourcePath + ".resourceKey", entry.ResourceKeyText);
            }
        }

        private static void ReportStaleBundles(GlobalResourceBuildPlan plan, string activeBuildTarget)
        {
            string bundleOutputPath = BundleRootPath + "/" + activeBuildTarget;
            if (!Directory.Exists(bundleOutputPath))
                return;

            var plannedBundles = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Bundles.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(plan.Bundles[i].BundleName))
                    plannedBundles.Add(plan.Bundles[i].BundleName);
            }

            string[] files = Directory.GetFiles(bundleOutputPath);
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                if (string.IsNullOrWhiteSpace(fileName) || fileName.EndsWith(".meta", StringComparison.Ordinal))
                    continue;

                if (!plannedBundles.Contains(fileName))
                    plan.Report.AddWarning("StaleGeneratedBundle", "Bundle file exists for this build target but is not selected by the current profile: " + files[i] + ".", "bundles/" + activeBuildTarget + "/" + fileName);
            }
        }

        private static void ValidateCompressionConsistency(GlobalResourceBuildPlan plan, GlobalResourceBuildBundle bundle, ref string selectedCompression)
        {
            string compression = string.IsNullOrWhiteSpace(bundle.Rule.compression) ? "lz4" : bundle.Rule.compression;
            if (string.IsNullOrWhiteSpace(selectedCompression))
            {
                selectedCompression = compression;
                return;
            }

            if (!string.Equals(selectedCompression, compression, StringComparison.OrdinalIgnoreCase))
            {
                plan.Report.AddError(
                    "MixedBundleCompressionUnsupported",
                    "Unity BuildPipeline.BuildAssetBundles applies compression per build call; mixed compression values require separate build passes.",
                    bundle.SourcePath + ".compression");
            }
        }

        private static void ValidateBuildTarget(GlobalResourceBuildPlan plan, GlobalResourceBuildBundle bundle, string activeBuildTarget)
        {
            string buildTarget = bundle.Rule.buildTarget ?? string.Empty;
            if (string.IsNullOrWhiteSpace(buildTarget) || string.Equals(buildTarget, "ActiveBuildTarget", StringComparison.Ordinal))
                return;

            if (!string.Equals(buildTarget, activeBuildTarget, StringComparison.Ordinal))
            {
                plan.Report.AddError(
                    "BuildTargetMismatch",
                    "Bundle rule buildTarget '" + buildTarget + "' does not match active build target '" + activeBuildTarget + "'.",
                    bundle.SourcePath + ".buildTarget");
            }
        }

        private static bool HasValidResourceKeyId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
                    continue;

                return false;
            }

            return true;
        }

        private static void SortPlan(GlobalResourceBuildPlan plan)
        {
            plan.Entries.Sort((left, right) => string.Compare(left.ResourceKeyText, right.ResourceKeyText, StringComparison.Ordinal));
            plan.Bundles.Sort((left, right) => string.Compare(left.BundleName, right.BundleName, StringComparison.Ordinal));
            for (int i = 0; i < plan.Bundles.Count; i++)
                plan.Bundles[i].Entries.Sort((left, right) => string.Compare(left.UnityAssetPath, right.UnityAssetPath, StringComparison.Ordinal));
        }

        private static AssetBundleBuild[] CreateAssetBundleBuilds(GlobalResourceBuildPlan plan)
        {
            var builds = new List<AssetBundleBuild>();
            for (int i = 0; i < plan.Bundles.Count; i++)
            {
                GlobalResourceBuildBundle bundle = plan.Bundles[i];
                if (bundle.Entries.Count == 0)
                    continue;

                var assetNames = new List<string>();
                for (int entryIndex = 0; entryIndex < bundle.Entries.Count; entryIndex++)
                {
                    string assetPath = bundle.Entries[entryIndex].UnityAssetPath;
                    if (!string.IsNullOrWhiteSpace(assetPath))
                        assetNames.Add(assetPath);
                }

                assetNames.Sort(StringComparer.Ordinal);
                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = bundle.BundleName,
                    assetNames = assetNames.ToArray()
                });
            }

            return builds.ToArray();
        }

        private static BuildAssetBundleOptions ResolveBuildOptions(GlobalResourceBuildPlan plan)
        {
            BuildAssetBundleOptions options = BuildAssetBundleOptions.None;
            for (int i = 0; i < plan.Bundles.Count; i++)
            {
                string compression = plan.Bundles[i].Rule.compression ?? string.Empty;
                if (string.Equals(compression, "uncompressed", StringComparison.OrdinalIgnoreCase))
                    options |= BuildAssetBundleOptions.UncompressedAssetBundle;
                else if (string.Equals(compression, "lz4", StringComparison.OrdinalIgnoreCase))
                    options |= BuildAssetBundleOptions.ChunkBasedCompression;
            }

            return options;
        }

        private static void CopyGeneratedBundles(GlobalResourceBuildPlan plan, string tempOutput, string bundleOutputPath)
        {
            for (int i = 0; i < plan.Bundles.Count; i++)
            {
                string bundleName = plan.Bundles[i].BundleName;
                string source = Path.Combine(tempOutput, bundleName);
                string destination = Path.Combine(bundleOutputPath, bundleName);
                if (!File.Exists(source))
                    throw new FileNotFoundException("Built AssetBundle was not found.", source);

                File.Copy(source, destination, true);
                AssetDatabase.ImportAsset(destination);
            }
        }

        private static ResourceCatalog CreateCatalog(GlobalResourceBuildPlan plan, string bundleOutputPath)
        {
            var entries = new List<ResourceCatalogEntry>();
            string buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            for (int bundleIndex = 0; bundleIndex < plan.Bundles.Count; bundleIndex++)
            {
                GlobalResourceBuildBundle bundle = plan.Bundles[bundleIndex];
                string bundlePath = Path.Combine(bundleOutputPath, bundle.BundleName);
                long size = File.Exists(bundlePath) ? new FileInfo(bundlePath).Length : 0;
                string hash = File.Exists(bundlePath) ? "sha256:" + ComputeSha256(bundlePath) : string.Empty;
                for (int entryIndex = 0; entryIndex < bundle.Entries.Count; entryIndex++)
                {
                    GlobalResourceResolvedEntry resolved = bundle.Entries[entryIndex];
                    entries.Add(new ResourceCatalogEntry(
                        resolved.ResourceKey.id,
                        resolved.TypeId,
                        AssetBundleProvider.Id,
                        bundle.BundleName + "|" + resolved.UnityAssetPath,
                        resolved.ResourceKey.variant,
                        string.IsNullOrWhiteSpace(resolved.ResourceKey.packageId) ? plan.Profile.packageId : resolved.ResourceKey.packageId,
                        CreateDependencies(resolved.Entry.dependencies),
                        CopyAndSort(resolved.Entry.labels),
                        hash,
                        size,
                        allowOverride: false,
                        providerData: CreateProviderData(plan, bundle, resolved, buildTarget)));
                }
            }

            entries.Sort((left, right) => string.Compare(left.CreateKey(plan.Profile.packageId).ToString(), right.CreateKey(plan.Profile.packageId).ToString(), StringComparison.Ordinal));
            return new ResourceCatalog(plan.Profile.catalogId, plan.Profile.packageId, entries);
        }

        private static ResourceCatalogValidationReport ValidateBundleFiles(GlobalResourceBuildPlan plan, ResourceCatalog catalog, string bundleOutputPath)
        {
            var report = new ResourceCatalogValidationReport();
            if (catalog == null)
                return report;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null || !string.Equals(entry.ProviderId, AssetBundleProvider.Id, StringComparison.Ordinal))
                    continue;

                ResourceKey key = entry.CreateKey(catalog.PackageId);
                if (!AssetBundleProvider.TryParseAddress(entry.Address, out string bundleName, out _))
                    continue;

                string bundlePath = Path.Combine(bundleOutputPath, bundleName);
                if (!File.Exists(bundlePath))
                    report.AddError("BundleMissing", key, "Global player AssetBundle was not found: " + bundlePath + ".");
            }

            return report;
        }

        private static string WritePreloadGroupsJson(GlobalResourceBuildPlan plan)
        {
            var groups = new List<PreloadGroupDto>();
            GlobalResourceBuildProfilePreloadGroupDocument[] sourceGroups = plan.Profile.preloadGroups ?? Array.Empty<GlobalResourceBuildProfilePreloadGroupDocument>();
            for (int i = 0; i < sourceGroups.Length; i++)
            {
                GlobalResourceBuildProfilePreloadGroupDocument group = sourceGroups[i];
                if (group == null)
                    continue;

                groups.Add(new PreloadGroupDto
                {
                    id = group.id,
                    explicitKeys = Copy(group.explicitKeys),
                    labels = CopyAndSort(group.labels),
                    failFast = group.failFast,
                    maxConcurrentLoads = group.maxConcurrentLoads
                });
            }

            groups.Sort((left, right) => string.Compare(left.id, right.id, StringComparison.Ordinal));
            return JsonConvert.SerializeObject(new PreloadGroupCatalogDto
            {
                schemaVersion = 1,
                profileId = plan.Profile.profileId,
                catalogId = plan.Profile.catalogId,
                groups = groups.ToArray()
            }, Formatting.Indented) + "\n";
        }

        private static string WriteBundleDependenciesJson(GlobalResourceBuildPlan plan, AssetBundleManifest manifest)
        {
            var bundles = new List<BundleDependencyDto>();
            for (int i = 0; i < plan.Bundles.Count; i++)
            {
                string bundleName = plan.Bundles[i].BundleName;
                string[] dependencies = plan.Bundles[i].Rule.includeDependencies
                    ? manifest.GetAllDependencies(bundleName) ?? Array.Empty<string>()
                    : Array.Empty<string>();
                Array.Sort(dependencies, StringComparer.Ordinal);
                bundles.Add(new BundleDependencyDto
                {
                    bundleName = bundleName,
                    dependencies = dependencies
                });
            }

            bundles.Sort((left, right) => string.Compare(left.bundleName, right.bundleName, StringComparison.Ordinal));
            return JsonConvert.SerializeObject(new BundleDependencyManifestDto
            {
                schemaVersion = 1,
                profileId = plan.Profile.profileId,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                bundles = bundles.ToArray()
            }, Formatting.Indented) + "\n";
        }

        private static string WriteBuildReportJson(GlobalResourceBuildPlan plan)
        {
            plan.Report.GeneratedAtUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            return JsonConvert.SerializeObject(plan.Report, Formatting.Indented) + "\n";
        }

        private static bool RuleMatchesEntry(GlobalResourceBuildProfileBundleRuleDocument rule, GlobalResourceResolvedEntry entry, string defaultPackageId)
        {
            if (!string.IsNullOrWhiteSpace(rule.id) && string.Equals(entry.Entry.bundleRule, rule.id, StringComparison.Ordinal))
                return true;
            if (ContainsAny(entry.Entry.labels, rule.matchLabels))
                return true;
            if (Contains(rule.matchPackageIds, string.IsNullOrWhiteSpace(entry.ResourceKey.packageId) ? defaultPackageId : entry.ResourceKey.packageId))
                return true;
            if (ContainsAny(entry.Entry.labels, PrefixValues("domain.", rule.matchDomains)))
                return true;

            ResourceKeyDto[] explicitKeys = rule.explicitKeys ?? Array.Empty<ResourceKeyDto>();
            for (int i = 0; i < explicitKeys.Length; i++)
            {
                if (ResourceKeysEqual(entry.ResourceKey, explicitKeys[i], defaultPackageId))
                    return true;
            }

            return false;
        }

        private static bool MatchesAnyBundleRule(GlobalResourceBuildPlan plan, GlobalResourceResolvedEntry entry)
        {
            GlobalResourceBuildProfileBundleRuleDocument[] rules = plan.Profile.bundleRules ?? Array.Empty<GlobalResourceBuildProfileBundleRuleDocument>();
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] != null && RuleMatchesEntry(rules[i], entry, plan.Profile.packageId))
                    return true;
            }

            return false;
        }

        private static bool ShouldUseDeclaredBundleRules(GlobalResourceBuildProfileEntryDocument entry)
        {
            if (entry == null)
                return false;
            if (entry.editorOnly || !entry.runtimeLoadable)
                return false;
            if (IsForcedInternalBundle(entry))
                return false;

            string overrideMode = string.IsNullOrWhiteSpace(entry.bundleOverrideMode) ? "none" : entry.bundleOverrideMode;
            if (string.Equals(overrideMode, "forceExternal", StringComparison.Ordinal) ||
                string.Equals(overrideMode, "exclude", StringComparison.Ordinal))
                return false;

            string deliveryMode = string.IsNullOrWhiteSpace(entry.deliveryMode) ? "internal" : entry.deliveryMode;
            return string.Equals(deliveryMode, "internal", StringComparison.Ordinal);
        }

        private static bool IsForcedInternalBundle(GlobalResourceBuildProfileEntryDocument entry)
        {
            if (entry == null)
                return false;

            return string.Equals(entry.bundleOverrideMode, "forceStandalone", StringComparison.Ordinal) ||
                string.Equals(entry.bundleOverrideMode, "forceBundle", StringComparison.Ordinal);
        }

        private static string GetForcedBundleName(GlobalResourceBuildProfileEntryDocument entry)
        {
            if (string.Equals(entry.bundleOverrideMode, "forceStandalone", StringComparison.Ordinal))
            {
                return !string.IsNullOrWhiteSpace(entry.bundleOverrideValue)
                    ? SanitizeBundleName(entry.bundleOverrideValue)
                    : "global.standalone." + SanitizeBundleSegment(entry.resourceKey != null ? entry.resourceKey.id : string.Empty);
            }

            return SanitizeBundleName(entry.bundleOverrideValue);
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

            var builder = new System.Text.StringBuilder(value.Length);
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

        private static bool ResourceKeysEqual(ResourceKeyDto left, ResourceKeyDto right, string defaultPackageId)
        {
            return string.Equals(left.id, right.id, StringComparison.Ordinal)
                && string.Equals(GetTypeId(left), GetTypeId(right), StringComparison.Ordinal)
                && string.Equals(left.variant ?? string.Empty, right.variant ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(string.IsNullOrWhiteSpace(left.packageId) ? defaultPackageId : left.packageId, string.IsNullOrWhiteSpace(right.packageId) ? defaultPackageId : right.packageId, StringComparison.Ordinal);
        }

        private static string CanonicalKey(ResourceKeyDto key, string defaultPackageId)
        {
            if (key == null || string.IsNullOrWhiteSpace(key.id) || string.IsNullOrWhiteSpace(GetTypeId(key)))
                return string.Empty;

            string packageId = string.IsNullOrWhiteSpace(key.packageId) ? defaultPackageId : key.packageId;
            return (packageId ?? string.Empty) + "|" + GetTypeId(key) + "|" + key.id + "|" + (key.variant ?? string.Empty);
        }

        private static bool IsSupportedCompression(string compression)
        {
            return string.IsNullOrWhiteSpace(compression)
                || string.Equals(compression, "lz4", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compression, "uncompressed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compression, "lzma", StringComparison.OrdinalIgnoreCase);
        }

        private static ResourceKey[] CreateDependencies(ResourceKeyDto[] dependencies)
        {
            if (dependencies == null || dependencies.Length == 0)
                return Array.Empty<ResourceKey>();

            var result = new ResourceKey[dependencies.Length];
            for (int i = 0; i < dependencies.Length; i++)
            {
                ResourceKeyDto dependency = dependencies[i];
                result[i] = new ResourceKey(dependency.id, GetTypeId(dependency), dependency.variant, dependency.packageId);
            }

            Array.Sort(result, (left, right) => string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal));
            return result;
        }

        private static Dictionary<string, string> CreateProviderData(GlobalResourceBuildPlan plan, GlobalResourceBuildBundle bundle, GlobalResourceResolvedEntry resolved, string buildTarget)
        {
            var data = resolved.Entry.providerData != null
                ? new Dictionary<string, string>(resolved.Entry.providerData, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            data["bundleName"] = bundle.BundleName;
            data["assetPath"] = resolved.UnityAssetPath;
            data["unityGuid"] = resolved.UnityGuid;
            data["buildProfileId"] = plan.Profile.profileId ?? string.Empty;
            data["buildTarget"] = buildTarget;
            data["unityMainObjectType"] = resolved.UnityMainObjectType;
            return data;
        }

        private static ResourceKeyDto[] Copy(ResourceKeyDto[] keys)
        {
            if (keys == null || keys.Length == 0)
                return Array.Empty<ResourceKeyDto>();

            var copy = new ResourceKeyDto[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                copy[i] = keys[i] != null ? keys[i].Clone() : new ResourceKeyDto();
            Array.Sort(copy, (left, right) => string.Compare(FormatKey(left), FormatKey(right), StringComparison.Ordinal));
            return copy;
        }

        private static string[] CopyAndSort(string[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<string>();

            var copy = (string[])values.Clone();
            Array.Sort(copy, StringComparer.Ordinal);
            return copy;
        }

        private static bool ContainsAny(string[] left, string[] right)
        {
            if (left == null || right == null)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (Contains(right, left[i]))
                    return true;
            }

            return false;
        }

        private static bool Contains(string[] values, string expected)
        {
            if (values == null || string.IsNullOrWhiteSpace(expected))
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string[] PrefixValues(string prefix, string[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<string>();

            var result = new List<string>();
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    result.Add(prefix + values[i]);
            }

            return result.ToArray();
        }

        private static string GetTypeId(ResourceKeyDto key)
        {
            if (key == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(key.type) ? key.type : (key.typeId ?? string.Empty);
        }

        private static string FormatKey(ResourceKeyDto key)
        {
            if (key == null)
                return string.Empty;

            return (key.packageId ?? string.Empty) + ":" + GetTypeId(key) + ":" + (key.id ?? string.Empty) + ":" + (key.variant ?? string.Empty);
        }

        public static string CreateReportText(GlobalResourceBuildReport report)
        {
            if (report == null || report.Issues.Count == 0)
                return "Global resource build profile report has no issues.";

            var lines = new List<string>();
            for (int i = 0; i < report.Issues.Count; i++)
            {
                GlobalResourceBuildIssue issue = report.Issues[i];
                lines.Add(issue.Severity + " " + issue.Code + " " + issue.SourcePath + " " + issue.ResourceKey + ": " + issue.Message);
            }

            return string.Join("\n", lines);
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string ComputeSha256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                char[] chars = new char[hash.Length * 2];
                for (int i = 0; i < hash.Length; i++)
                {
                    byte value = hash[i];
                    chars[i * 2] = GetHex(value >> 4);
                    chars[(i * 2) + 1] = GetHex(value & 0xF);
                }

                return new string(chars);
            }
        }

        private static char GetHex(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + value - 10);
        }

        public sealed class GlobalResourceBuildPlan
        {
            public GlobalResourceBuildPlan(GlobalResourceBuildProfileDocument profile, GlobalResourceBuildReport report)
            {
                Profile = profile;
                Report = report;
            }

            public GlobalResourceBuildProfileDocument Profile { get; }
            public GlobalResourceBuildReport Report { get; }
            public List<GlobalResourceResolvedEntry> Entries { get; } = new List<GlobalResourceResolvedEntry>();
            public List<GlobalResourceBuildBundle> Bundles { get; } = new List<GlobalResourceBuildBundle>();
            public bool HasErrors => Report != null && Report.HasErrors;
        }

        public sealed class GlobalResourceResolvedEntry
        {
            public GlobalResourceResolvedEntry(GlobalResourceBuildProfileEntryDocument entry, string sourcePath)
            {
                Entry = entry;
                SourcePath = sourcePath ?? string.Empty;
            }

            public GlobalResourceBuildProfileEntryDocument Entry { get; }
            public string SourcePath { get; }
            public ResourceKeyDto ResourceKey => Entry.resourceKey ?? new ResourceKeyDto();
            public string TypeId => GetTypeId(ResourceKey);
            public string ResourceKeyText => FormatKey(ResourceKey);
            public bool RuntimeLoadable => Entry.runtimeLoadable && !Entry.editorOnly;
            public string UnityAssetPath { get; set; } = string.Empty;
            public string UnityGuid { get; set; } = string.Empty;
            public string UnityMainObjectType { get; set; } = string.Empty;
        }

        public sealed class GlobalResourceBuildBundle
        {
            public GlobalResourceBuildBundle(GlobalResourceBuildProfileBundleRuleDocument rule, string sourcePath)
            {
                Rule = rule;
                SourcePath = sourcePath ?? string.Empty;
            }

            public GlobalResourceBuildProfileBundleRuleDocument Rule { get; }
            public string SourcePath { get; }
            public string BundleName => Rule.bundleName ?? string.Empty;
            public List<GlobalResourceResolvedEntry> Entries { get; } = new List<GlobalResourceResolvedEntry>();
        }

        public sealed class GlobalResourceBuildReport
        {
            public string ProfileId { get; set; } = string.Empty;
            public string CatalogId { get; set; } = string.Empty;
            public string PackageId { get; set; } = string.Empty;
            public string BuildTarget { get; set; } = string.Empty;
            public string GeneratedAtUtc { get; set; } = string.Empty;
            public List<GlobalResourceBuildIssue> Issues { get; set; } = new List<GlobalResourceBuildIssue>();

            public bool HasErrors
            {
                get
                {
                    for (int i = 0; i < Issues.Count; i++)
                    {
                        if (string.Equals(Issues[i].Severity, "Error", StringComparison.Ordinal))
                            return true;
                    }

                    return false;
                }
            }

            public void AddError(string code, string message, string sourcePath, string resourceKey = "")
            {
                Add("Error", code, message, sourcePath, resourceKey);
            }

            public void AddWarning(string code, string message, string sourcePath, string resourceKey = "")
            {
                Add("Warning", code, message, sourcePath, resourceKey);
            }

            private void Add(string severity, string code, string message, string sourcePath, string resourceKey)
            {
                Issues.Add(new GlobalResourceBuildIssue
                {
                    Severity = severity,
                    Code = code ?? string.Empty,
                    Message = message ?? string.Empty,
                    SourcePath = sourcePath ?? string.Empty,
                    ResourceKey = resourceKey ?? string.Empty
                });
            }
        }

        public sealed class GlobalResourceBuildIssue
        {
            public string Severity { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string SourcePath { get; set; } = string.Empty;
            public string ResourceKey { get; set; } = string.Empty;
        }

        public sealed class GlobalResourceBuildProfileDocument
        {
            public int schemaVersion = 1;
            public string profileId;
            public string catalogId;
            public string packageId;
            public GlobalResourceBuildProfileEntryDocument[] entries;
            public GlobalResourceBuildProfileBundleRuleDocument[] bundleRules;
            public GlobalResourceBuildProfilePreloadGroupDocument[] preloadGroups;
        }

        public sealed class GlobalResourceBuildProfileEntryDocument
        {
            public ResourceKeyDto resourceKey;
            public GlobalResourceBuildProfileEntrySourceDocument source;
            public string[] labels;
            public string bundleRule;
            public string deliveryMode = "internal";
            public string bundleOverrideMode = "none";
            public string bundleOverrideValue;
            public string bundleGroupHint;
            public string[] preloadGroups;
            public ResourceKeyDto[] dependencies;
            public Dictionary<string, string> providerData;
            public bool runtimeLoadable = true;
            public bool editorOnly;
        }

        public sealed class GlobalResourceBuildProfileEntrySourceDocument
        {
            public string providerId;
            public string unityAssetPath;
            public string unityGuid;
        }

        public sealed class GlobalResourceBuildProfileBundleRuleDocument
        {
            public string id;
            public string bundleName;
            public ResourceKeyDto[] explicitKeys;
            public string[] matchLabels;
            public string[] matchDomains;
            public string[] matchPackageIds;
            public string compression = "lz4";
            public string buildTarget = "ActiveBuildTarget";
            public bool includeDependencies = true;
            public bool allowEmpty;
        }

        public sealed class GlobalResourceBuildProfilePreloadGroupDocument
        {
            public string id;
            public ResourceKeyDto[] explicitKeys;
            public string[] labels;
            public bool failFast = true;
            public int maxConcurrentLoads = 4;
        }

        public sealed class ResourceKeyDto
        {
            public string id;
            public string type;
            public string typeId;
            public string variant;
            public string packageId;

            public ResourceKeyDto Clone()
            {
                return new ResourceKeyDto
                {
                    id = id,
                    type = type,
                    typeId = typeId,
                    variant = variant,
                    packageId = packageId
                };
            }
        }

        private sealed class PreloadGroupCatalogDto
        {
            public int schemaVersion;
            public string profileId;
            public string catalogId;
            public PreloadGroupDto[] groups;
        }

        private sealed class PreloadGroupDto
        {
            public string id;
            public ResourceKeyDto[] explicitKeys;
            public string[] labels;
            public bool failFast;
            public int maxConcurrentLoads;
        }

        private sealed class BundleDependencyManifestDto
        {
            public int schemaVersion;
            public string profileId;
            public string buildTarget;
            public BundleDependencyDto[] bundles;
        }

        private sealed class BundleDependencyDto
        {
            public string bundleName;
            public string[] dependencies;
        }
    }
}
