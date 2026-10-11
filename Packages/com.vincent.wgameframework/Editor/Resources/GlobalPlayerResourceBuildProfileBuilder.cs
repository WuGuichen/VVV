using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Linq;
using MxFramework.ResourceMapping.Authoring;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Production Player artifact emitter over the sole v2 resource mapping compiler.</summary>
    public static class GlobalPlayerResourceBuildProfileBuilder
    {
        public const string ProfilePath = GlobalResourceMappingBuilder.ProfilePath;
        public const string CatalogPath = "Assets/StreamingAssets/MxFramework/Resources/global_runtime_catalog.json";
        public const string CatalogMountsPath = "Assets/StreamingAssets/MxFramework/Resources/global_catalog_mounts.json";
        public const string PreloadGroupsPath = "Assets/StreamingAssets/MxFramework/Resources/global_preload_groups.json";
        public const string BundleDependenciesPath = "Assets/StreamingAssets/MxFramework/Resources/global_bundle_dependencies.json";
        public const string BuildReportPath = "Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json";
        public const string BundleRootPath = "Assets/StreamingAssets/MxFramework/Resources/Bundles";
        public const string ArtifactSetPath = "Assets/StreamingAssets/MxFramework/Resources/global_resource_artifact_set.json";

        [MenuItem("MxFramework/Resources/Validate Global Resource Mapping", priority = 130)]
        public static void ValidateGlobalProfile()
        {
            GlobalResourceBuildPlan plan = CreateBuildPlan(ProfilePath);
            if (plan.HasErrors) Debug.LogError(CreateReportText(plan.Report));
            else Debug.Log("Global resource mapping validated. Plan fingerprint: " + plan.Compilation.Plan.BuildPlanFingerprint + ".");
        }

        [MenuItem("MxFramework/Resources/Build Global Player Resources", priority = 131)]
        public static void BuildGlobalPlayerResourceCatalog()
        {
            GlobalResourceBuildPlan plan = CreateBuildPlan(ProfilePath);
            if (plan.HasErrors) { Debug.LogError(CreateReportText(plan.Report)); return; }
            Build(plan);
            Debug.Log("Global v2 Player resource artifacts generated: " + CatalogPath + ".");
        }

        /// <remarks>AllocByDesign. Editor-only production planning and Unity source resolution.</remarks>
        public static GlobalResourceBuildPlan CreateBuildPlan(string profilePath)
        {
            GlobalResourceMappingBuildInput input = GlobalResourceMappingBuilder.CreateBuildInput(profilePath);
            var plan = new GlobalResourceBuildPlan { Input = input, Compilation = input.Compilation };
            if (input.Compilation != null)
            {
                plan.Report.ProfileId = input.Compilation.Plan.ProfileId;
                plan.Report.CatalogId = input.Compilation.Plan.CatalogId;
                plan.Report.BuildTarget = input.Compilation.Plan.BuildTarget;
                plan.Report.InternalEntryCount = input.Compilation.Plan.Counts.Internal;
                plan.Report.ExternalEntryCount = input.Compilation.Plan.Counts.External;
                plan.Report.ExcludedEntryCount = input.Compilation.Plan.Counts.Excluded;
                var resolvedByIdentity = new Dictionary<string, GlobalResourceMappingV2ResolvedSource>(StringComparer.Ordinal);
                for (int i = 0; i < input.ResolvedSources.Count; i++) resolvedByIdentity[input.ResolvedSources[i].ResourceKeyIdentity] = input.ResolvedSources[i];
                for (int i = 0; i < input.Compilation.Plan.Entries.Count; i++)
                {
                    GlobalResourceBuildPlanEntry source = input.Compilation.Plan.Entries[i];
                    resolvedByIdentity.TryGetValue(source.ResourceKey != null ? source.ResourceKey.ToIdentityString() : string.Empty, out GlobalResourceMappingV2ResolvedSource resolved);
                    var entry = new GlobalResourceResolvedEntry { Entry = source, Source = resolved };
                    plan.Entries.Add(entry);
                    if (source.DeliveryIntent == ResourceMappingDeliveryIntent.External) plan.ExternalEntries.Add(entry);
                    if (source.DeliveryIntent == ResourceMappingDeliveryIntent.Excluded) plan.ExcludedEntries.Add(entry);
                    if (source.DeliveryIntent == ResourceMappingDeliveryIntent.Internal && source.ResourceKey != null && source.ResourceKey.TypeId == ResourceTypeIds.Scene) plan.Report.SceneEntryCount++;
                }
                for (int i = 0; i < input.Compilation.Plan.Bundles.Count; i++)
                {
                    GlobalResourceBuildPlanBundle sourceBundle = input.Compilation.Plan.Bundles[i];
                    var bundle = new GlobalResourceBuildBundle { Bundle = sourceBundle };
                    for (int keyIndex = 0; keyIndex < sourceBundle.ResourceKeys.Count; keyIndex++)
                    {
                        string identity = sourceBundle.ResourceKeys[keyIndex].ToIdentityString();
                        GlobalResourceResolvedEntry found = plan.Entries.Find(candidate => candidate.Entry.ResourceKey != null && candidate.Entry.ResourceKey.ToIdentityString() == identity);
                        if (found != null) bundle.Entries.Add(found);
                    }
                    plan.Bundles.Add(bundle);
                }
            }
            for (int i = 0; i < input.Diagnostics.Count; i++)
            {
                ResourceMappingDiagnostic diagnostic = input.Diagnostics[i];
                plan.Report.Issues.Add(new GlobalResourceBuildIssue { Severity = diagnostic.Severity.ToString(), Code = diagnostic.Code, Message = diagnostic.Message, SourcePath = diagnostic.Path });
            }
            return plan;
        }

        /// <summary>Verify a committed generation before a Player reuses it.</summary>
        public static void ValidatePublishedArtifacts(GlobalResourceBuildPlan plan)
        {
            if (plan == null || plan.HasErrors || plan.Compilation == null)
                throw new InvalidOperationException("Current global resource build plan is invalid.");
            GeneratedResourceArtifactSet marker = GeneratedResourceArtifactSetLoader.LoadFromFile(
                Path.GetFullPath(ArtifactSetPath), Path.GetFullPath("Assets/StreamingAssets"));
            if (!File.Exists(BuildReportPath))
                throw new InvalidOperationException("Global resource build report is missing: " + BuildReportPath);
            JObject report = JObject.Parse(File.ReadAllText(BuildReportPath));
            RequirePublishedIdentity("profileId", plan.Compilation.Plan.ProfileId, marker.ProfileId, report);
            RequirePublishedIdentity("catalogId", plan.Compilation.Plan.CatalogId, marker.CatalogId, report);
            RequirePublishedIdentity("buildTarget", plan.Compilation.Plan.BuildTarget, marker.BuildTarget, report);
            RequirePublishedIdentity("mappingInputFingerprint", plan.Compilation.Plan.MappingInputFingerprint,
                marker.MappingInputFingerprint, report);
            RequirePublishedIdentity("buildPlanFingerprint", plan.Compilation.Plan.BuildPlanFingerprint,
                marker.BuildPlanFingerprint, report);
            RequirePublishedIdentity("sourceInputFingerprint", plan.Input.SourceInputFingerprint,
                marker.SourceInputFingerprint, report);
        }

        public static string DescribeBuildInput(GlobalResourceBuildPlan plan)
        {
            if (plan == null || plan.HasErrors || plan.Compilation == null)
                throw new InvalidOperationException("Current global resource build plan is invalid.");
            return "profileId=" + plan.Compilation.Plan.ProfileId + "\n"
                + "catalogId=" + plan.Compilation.Plan.CatalogId + "\n"
                + "buildTarget=" + plan.Compilation.Plan.BuildTarget + "\n"
                + "mappingInputFingerprint=" + plan.Compilation.Plan.MappingInputFingerprint + "\n"
                + "buildPlanFingerprint=" + plan.Compilation.Plan.BuildPlanFingerprint + "\n"
                + "sourceInputFingerprint=" + plan.Input.SourceInputFingerprint + "\n";
        }

        private static void RequirePublishedIdentity(string field, string expected, string markerValue, JObject report)
        {
            if (string.IsNullOrWhiteSpace(expected)
                || !string.Equals(expected, markerValue, StringComparison.Ordinal)
                || !string.Equals(expected, (string)report[field], StringComparison.Ordinal))
                throw new InvalidOperationException("Published global resource " + field
                    + " does not match current input, artifact set and build report.");
        }

        /// <remarks>AllocByDesign. Editor-only AssetBundle and Player artifact emission.</remarks>
        public static void Build(GlobalResourceBuildPlan plan)
        {
            Build(plan, Array.Empty<ResourceCatalog>(), new[] { AssetBundleProvider.ExternalId });
        }

        /// <remarks>AllocByDesign. Editor-only emission with explicit composition-root Catalog and Provider evidence.</remarks>
        public static void Build(
            GlobalResourceBuildPlan plan,
            IEnumerable<ResourceCatalog> compositionRootCatalogs,
            IEnumerable<string> availableProviderIds)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.HasErrors) throw new InvalidOperationException("Global resource build plan has validation errors.");
            string target = EditorUserBuildSettings.activeBuildTarget.ToString();
            if (!string.Equals(plan.Compilation.Plan.BuildTarget, target, StringComparison.Ordinal)) throw new InvalidOperationException("Compiled build target does not match Unity active build target.");
            GlobalResourceBundleResidueLedger.Context residue =
                GlobalResourceBundleResidueLedger.Prepare(plan.Compilation.Plan.ProfileId, target);
            if (!string.IsNullOrWhiteSpace(residue.Issue)) Debug.LogWarning(residue.Issue);
            string output = BundleRootPath + "/" + target;
            BuildAssetBundleOptions buildOptions = ResolveBuildOptions(plan);
            string temporary = GlobalPlayerResourceBuildContext.PrepareBundleDirectory(
                plan.Compilation.Plan.ProfileId, plan.Compilation.Plan.BuildPlanFingerprint, buildOptions);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            AssetBundleManifest manifest;
            try { manifest = BuildPipeline.BuildAssetBundles(temporary, CreateAssetBundleBuilds(plan), buildOptions, EditorUserBuildSettings.activeBuildTarget); }
            catch { GlobalPlayerResourceBuildContext.CaptureBundleFailure(temporary, timer.Elapsed.TotalSeconds); throw; }
            timer.Stop();
            if (manifest == null)
            {
                GlobalPlayerResourceBuildContext.CaptureBundleFailure(temporary, timer.Elapsed.TotalSeconds);
                throw new InvalidOperationException("Unity failed to build global resource AssetBundles.");
            }
            GlobalPlayerResourceBuildContext.CaptureBundles(temporary, plan.Input.SourceInputFingerprint,
                plan.Compilation.Plan.BuildPlanFingerprint, timer.Elapsed.TotalSeconds);
            ResourceCatalog catalog = CreateCatalog(plan, temporary);
            ResourceCatalogValidationReport report = ResourceCatalogEditorValidator.ValidateCatalog(catalog, new[] { AssetBundleProvider.Id, SceneAssetBundleProvider.Id });
            if (report.HasErrors) throw new InvalidOperationException(ResourceCatalogEditorValidator.CreateReportText(catalog, report));
            string mountJson = CreateCatalogMountManifestJson(plan);
            ResolvedResourceCatalogMountSet mounted = GeneratedResourceCatalogMountLoader.ResolveFromJson(mountJson, Path.GetFullPath("Assets/StreamingAssets"),
                compositionRootCatalogs, availableProviderIds);
            if (!string.Equals(mounted.MappingInputFingerprint, plan.Compilation.Plan.MappingInputFingerprint, StringComparison.Ordinal) ||
                !string.Equals(mounted.BuildPlanFingerprint, plan.Compilation.Plan.BuildPlanFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException("CatalogMount manifest does not belong to the compiled build plan.");

            string stageRoot = Path.Combine("Temp", "MxFrameworkGlobalResourcePublish", Guid.NewGuid().ToString("N"), "StreamingAssets");
            var artifacts = new List<PublishedArtifact>();
            StageText(stageRoot, ToStreamingRelative(CatalogPath), SampleResourceCatalogBuilder.WriteCatalogJson(catalog), "catalog", artifacts);
            StageText(stageRoot, ToStreamingRelative(CatalogMountsPath), mountJson, "catalogMounts", artifacts);
            StageText(stageRoot, ToStreamingRelative(PreloadGroupsPath), WritePreloadGroupsJson(plan), "preloadGroups", artifacts);
            StageText(stageRoot, ToStreamingRelative(BundleDependenciesPath), WriteBundleDependenciesJson(plan, manifest), "bundleDependencies", artifacts);
            StageBundles(plan, temporary, stageRoot, target, artifacts);
            StageText(stageRoot, ToStreamingRelative(ArtifactSetPath), WriteArtifactSetJson(plan, artifacts), "artifactSet", null);

            string destinationRoot = Path.GetFullPath("Assets/StreamingAssets");
            for (int i = 0; i < artifacts.Count; i++) PublishAtomically(Path.Combine(stageRoot, artifacts[i].RelativePath), Path.Combine(destinationRoot, artifacts[i].RelativePath));
            PublishAtomically(Path.Combine(stageRoot, ToStreamingRelative(ArtifactSetPath)), Path.GetFullPath(ArtifactSetPath));
            PublishTextAtomically(BuildReportPath, WriteBuildReportJson(plan, output));
            try
            {
                GlobalResourceBundleResidueLedger.RecordCommittedMarker(residue);
                GlobalResourceBundleResidueLedger.CleanupCommitted(residue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Global resource bundle retirement could not complete: " + ex.Message);
            }
            try
            {
                string residueReport = GlobalResourceBundleResidueLedger.CreateReport(
                    plan.Compilation.Plan.ProfileId, target);
                Directory.CreateDirectory(Path.GetDirectoryName(GlobalResourceBundleResidueLedger.ReportPath) ?? ".");
                File.WriteAllText(GlobalResourceBundleResidueLedger.ReportPath, residueReport);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Global resource bundle residue report could not be written: " + ex.Message);
            }
            AssetDatabase.Refresh();
        }

        public static ResourceCatalog CreateCatalog(GlobalResourceBuildPlan plan, string bundleOutputPath)
        {
            var entries = new List<ResourceCatalogEntry>();
            for (int bundleIndex = 0; bundleIndex < plan.Bundles.Count; bundleIndex++)
            {
                GlobalResourceBuildBundle bundle = plan.Bundles[bundleIndex];
                string bundlePath = Path.Combine(bundleOutputPath ?? string.Empty, bundle.Bundle.BundleName);
                string hash = File.Exists(bundlePath) ? "sha256:" + ComputeSha256(bundlePath) : string.Empty;
                long size = File.Exists(bundlePath) ? new FileInfo(bundlePath).Length : 0;
                for (int entryIndex = 0; entryIndex < bundle.Entries.Count; entryIndex++)
                {
                    GlobalResourceResolvedEntry resolved = bundle.Entries[entryIndex];
                    GlobalResourceBuildPlanEntry source = resolved.Entry;
                    entries.Add(new ResourceCatalogEntry(source.ResourceKey.Id, source.ResourceKey.TypeId,
                        source.ResourceKey.TypeId == ResourceTypeIds.Scene ? SceneAssetBundleProvider.Id : AssetBundleProvider.Id,
                        bundle.Bundle.BundleName + "|" + resolved.Source.UnityAssetPath, source.ResourceKey.Variant, source.ResourceKey.PackageId,
                        CreateDependencies(source.Dependencies), source.Labels, hash, size, source.OverrideIntent == ResourceMappingOverrideIntent.Allow,
                        CreateProviderData(plan, bundle, resolved)));
                }
            }
            entries.Sort((left, right) => string.Compare(left.CreateKey().ToString(), right.CreateKey().ToString(), StringComparison.Ordinal));
            return new ResourceCatalog(plan.Compilation.Plan.CatalogId, string.Empty, entries);
        }

        public static ResourceCatalog CreateEditorAssetDatabaseCatalog(GlobalResourceBuildPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var entries = new List<ResourceCatalogEntry>();
            for (int i = 0; i < plan.Entries.Count; i++)
            {
                GlobalResourceResolvedEntry resolved = plan.Entries[i];
                if (resolved.Entry.DeliveryIntent != ResourceMappingDeliveryIntent.Internal || resolved.Entry.ResourceKey == null || resolved.Source == null) continue;
                bool scene = resolved.Entry.ResourceKey.TypeId == ResourceTypeIds.Scene;
                entries.Add(new ResourceCatalogEntry(resolved.Entry.ResourceKey.Id, resolved.Entry.ResourceKey.TypeId,
                    scene ? EditorSceneAssetDatabaseResourceProvider.Id : EditorAssetDatabaseResourceProvider.Id,
                    resolved.Source.UnityAssetPath, resolved.Entry.ResourceKey.Variant, resolved.Entry.ResourceKey.PackageId,
                    CreateDependencies(resolved.Entry.Dependencies), resolved.Entry.Labels, allowOverride: resolved.Entry.OverrideIntent == ResourceMappingOverrideIntent.Allow,
                    providerData: new Dictionary<string, string> { { "assetPath", resolved.Source.UnityAssetPath }, { "unityGuid", resolved.Source.UnityGuid }, { "buildPlanFingerprint", plan.Compilation.Plan.BuildPlanFingerprint } }));
            }
            return new ResourceCatalog(plan.Compilation.Plan.CatalogId + ".editor", string.Empty, entries);
        }

        public static GeneratedResourcePreloadGroupCatalog CreatePreloadGroupCatalog(GlobalResourceBuildPlan plan)
        {
            var groups = new List<ResourcePreloadPlan>();
            for (int i = 0; i < plan.Compilation.Plan.PreloadGroups.Count; i++)
            {
                GlobalResourceBuildPlanPreloadGroup source = plan.Compilation.Plan.PreloadGroups[i];
                groups.Add(new ResourcePreloadPlan(source.Id, CreateDependencies(source.ResourceKeys), Array.Empty<string>(), source.FailFast, source.MaxConcurrentLoads));
            }
            return new GeneratedResourcePreloadGroupCatalog(plan.Compilation.Plan.ProfileId, plan.Compilation.Plan.CatalogId, groups,
                plan.Compilation.Plan.MappingInputFingerprint, plan.Compilation.Plan.BuildPlanFingerprint);
        }

        public static string CreateReportText(GlobalResourceBuildReport report)
        {
            if (report == null) return "Resource build report is missing.";
            var lines = new List<string> { "Profile: " + report.ProfileId, "Catalog: " + report.CatalogId, "Build target: " + report.BuildTarget };
            for (int i = 0; i < report.Issues.Count; i++) lines.Add(report.Issues[i].Severity + " " + report.Issues[i].Code + ": " + report.Issues[i].Message + " (" + report.Issues[i].SourcePath + ")");
            return string.Join("\n", lines);
        }

        private static AssetBundleBuild[] CreateAssetBundleBuilds(GlobalResourceBuildPlan plan)
        {
            var builds = new List<AssetBundleBuild>();
            for (int i = 0; i < plan.Bundles.Count; i++)
            {
                var paths = new List<string>();
                for (int j = 0; j < plan.Bundles[i].Entries.Count; j++) if (plan.Bundles[i].Entries[j].Source != null) paths.Add(plan.Bundles[i].Entries[j].Source.UnityAssetPath);
                paths.Sort(StringComparer.Ordinal);
                if (paths.Count == 0) continue;
                builds.Add(new AssetBundleBuild { assetBundleName = plan.Bundles[i].Bundle.BundleName, assetNames = paths.ToArray() });
            }
            return builds.ToArray();
        }

        private static BuildAssetBundleOptions ResolveBuildOptions(GlobalResourceBuildPlan plan)
        {
            string compression = plan.Bundles.Count > 0 ? plan.Bundles[0].Bundle.Compression : "lz4";
            if (string.Equals(compression, "lz4", StringComparison.OrdinalIgnoreCase)) return BuildAssetBundleOptions.ChunkBasedCompression;
            if (string.Equals(compression, "uncompressed", StringComparison.OrdinalIgnoreCase)) return BuildAssetBundleOptions.UncompressedAssetBundle;
            if (string.Equals(compression, "lzma", StringComparison.OrdinalIgnoreCase)) return BuildAssetBundleOptions.None;
            throw new InvalidOperationException("Unsupported Bundle compression: " + compression + ".");
        }

        private static Dictionary<string, string> CreateProviderData(GlobalResourceBuildPlan plan, GlobalResourceBuildBundle bundle, GlobalResourceResolvedEntry entry) => new()
        {
            { "bundleName", bundle.Bundle.BundleName }, { "assetPath", entry.Source.UnityAssetPath }, { "buildTarget", plan.Compilation.Plan.BuildTarget },
            { "mappingInputFingerprint", plan.Compilation.Plan.MappingInputFingerprint }, { "buildPlanFingerprint", plan.Compilation.Plan.BuildPlanFingerprint }
        };

        private static ResourceKey[] CreateDependencies(IReadOnlyList<ResourceMappingResourceKey> source)
        {
            var result = new ResourceKey[source != null ? source.Count : 0];
            for (int i = 0; i < result.Length; i++) result[i] = new ResourceKey(source[i].Id, source[i].TypeId, source[i].Variant, source[i].PackageId);
            return result;
        }

        private static string WritePreloadGroupsJson(GlobalResourceBuildPlan plan)
        {
            var groups = new List<object>();
            for (int i = 0; i < plan.Compilation.Plan.PreloadGroups.Count; i++)
            {
                GlobalResourceBuildPlanPreloadGroup group = plan.Compilation.Plan.PreloadGroups[i];
                groups.Add(new { id = group.Id, explicitKeys = group.ResourceKeys.ConvertAll(KeyDto), labels = Array.Empty<string>(), failFast = group.FailFast, maxConcurrentLoads = group.MaxConcurrentLoads });
            }
            return JsonConvert.SerializeObject(new
            {
                schemaVersion = 1, profileId = plan.Compilation.Plan.ProfileId, catalogId = plan.Compilation.Plan.CatalogId,
                mappingInputFingerprint = plan.Compilation.Plan.MappingInputFingerprint, buildPlanFingerprint = plan.Compilation.Plan.BuildPlanFingerprint,
                groups
            }, Formatting.Indented) + "\n";
        }

        /// <remarks>AllocByDesign. Produces the runtime CatalogMount allowlist consumed by Editor and Player bootstrap.</remarks>
        public static string CreateCatalogMountManifestJson(GlobalResourceBuildPlan plan)
        {
            var mounts = new List<object>();
            for (int i = 0; i < plan.Compilation.Plan.CatalogMounts.Count; i++)
            {
                GlobalResourceBuildPlanCatalogMount mount = plan.Compilation.Plan.CatalogMounts[i];
                mounts.Add(new
                {
                    catalogMountId = mount.CatalogMountId,
                    catalogId = mount.CatalogId,
                    bootstrapKind = mount.BootstrapKind.ToString(),
                    relativeCatalogPath = mount.RelativeCatalogPath,
                    mountOrder = mount.MountOrder,
                    expectedCatalogFingerprint = mount.ExpectedCatalogFingerprint,
                    allowlistedKeys = mount.AllowlistedKeys.ConvertAll(entry => new
                    {
                        id = entry.ResourceKey.Id,
                        type = entry.ResourceKey.TypeId,
                        variant = entry.ResourceKey.Variant,
                        packageId = entry.ResourceKey.PackageId,
                        allowOverride = entry.OverrideIntent == ResourceMappingOverrideIntent.Allow
                    })
                });
            }
            return JsonConvert.SerializeObject(new
            {
                schemaVersion = 1, profileId = plan.Compilation.Plan.ProfileId, catalogId = plan.Compilation.Plan.CatalogId,
                mappingInputFingerprint = plan.Compilation.Plan.MappingInputFingerprint, buildPlanFingerprint = plan.Compilation.Plan.BuildPlanFingerprint,
                mounts
            }, Formatting.Indented) + "\n";
        }

        private static string WriteBundleDependenciesJson(GlobalResourceBuildPlan plan, AssetBundleManifest manifest)
        {
            var bundles = new List<object>();
            for (int i = 0; i < plan.Bundles.Count; i++)
            {
                GlobalResourceBuildBundle bundle = plan.Bundles[i];
                string[] dependencies = bundle.Bundle.IncludeDependencies
                    ? manifest.GetAllDependencies(bundle.Bundle.BundleName) ?? Array.Empty<string>()
                    : Array.Empty<string>();
                bundles.Add(new { bundleName = bundle.Bundle.BundleName, dependencies });
            }
            return JsonConvert.SerializeObject(new
            {
                schemaVersion = 1, profileId = plan.Compilation.Plan.ProfileId, buildTarget = plan.Compilation.Plan.BuildTarget,
                mappingInputFingerprint = plan.Compilation.Plan.MappingInputFingerprint, buildPlanFingerprint = plan.Compilation.Plan.BuildPlanFingerprint,
                bundles
            }, Formatting.Indented) + "\n";
        }

        private static string WriteBuildReportJson(GlobalResourceBuildPlan plan, string output) => JsonConvert.SerializeObject(new
        {
            schemaVersion = 2, profileId = plan.Compilation.Plan.ProfileId, catalogId = plan.Compilation.Plan.CatalogId, buildTarget = plan.Compilation.Plan.BuildTarget,
            mappingInputFingerprint = plan.Compilation.Plan.MappingInputFingerprint, buildPlanFingerprint = plan.Compilation.Plan.BuildPlanFingerprint,
            sourceInputFingerprint = plan.Input.SourceInputFingerprint, catalogPath = CatalogPath, catalogMountsPath = CatalogMountsPath,
            artifactSetPath = ArtifactSetPath, bundleRootPath = output,
            residueReportPath = GlobalResourceBundleResidueLedger.ReportPath,
            counts = plan.Compilation.Plan.Counts
        }, Formatting.Indented) + "\n";

        private static string WriteArtifactSetJson(GlobalResourceBuildPlan plan, List<PublishedArtifact> artifacts)
        {
            return JsonConvert.SerializeObject(new
            {
                schemaVersion = 1,
                profileId = plan.Compilation.Plan.ProfileId,
                catalogId = plan.Compilation.Plan.CatalogId,
                buildTarget = plan.Compilation.Plan.BuildTarget,
                mappingInputFingerprint = plan.Compilation.Plan.MappingInputFingerprint,
                buildPlanFingerprint = plan.Compilation.Plan.BuildPlanFingerprint,
                sourceInputFingerprint = plan.Input.SourceInputFingerprint,
                artifacts = artifacts.Select(item => new { kind = item.Kind, relativePath = item.RelativePath, sha256 = item.Sha256 }).ToArray()
            }, Formatting.Indented) + "\n";
        }

        private static void StageText(string root, string relativePath, string content, string kind, List<PublishedArtifact> artifacts)
        {
            string path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
            File.WriteAllText(path, content);
            if (artifacts != null) artifacts.Add(new PublishedArtifact(kind, relativePath, "sha256:" + ComputeSha256(path)));
        }

        private static void StageBundles(GlobalResourceBuildPlan plan, string temporary, string stageRoot, string target, List<PublishedArtifact> artifacts)
        {
            for (int i = 0; i < plan.Bundles.Count; i++)
            {
                if (plan.Bundles[i].Entries.Count == 0) continue;
                string name = plan.Bundles[i].Bundle.BundleName;
                string relative = "MxFramework/Resources/Bundles/" + target + "/" + name;
                string destination = Path.Combine(stageRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? stageRoot);
                File.Copy(Path.Combine(temporary, name), destination, true);
                artifacts.Add(new PublishedArtifact("bundle", relative, "sha256:" + ComputeSha256(destination)));
            }
        }

        private static string ToStreamingRelative(string projectPath)
        {
            const string prefix = "Assets/StreamingAssets/";
            if (!projectPath.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Artifact is outside StreamingAssets: " + projectPath + ".");
            return projectPath.Substring(prefix.Length);
        }

        private static void PublishTextAtomically(string path, string content)
        {
            string pending = path + ".pending";
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(pending, content);
            ReplaceFile(pending, path);
        }

        private static void PublishAtomically(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
            string pending = destination + ".pending";
            File.Copy(source, pending, true);
            ReplaceFile(pending, destination);
        }

        private static void ReplaceFile(string pending, string destination)
        {
            const int maxAttempts = 5;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(destination))
                        File.Replace(pending, destination, null);
                    else
                        File.Move(pending, destination);
                    return;
                }
                catch (IOException) when (attempt + 1 < maxAttempts)
                {
                    // Unity importers and antivirus scanners can briefly retain a
                    // read handle. This is low-frequency Editor publication only.
                    Thread.Sleep(20 * (attempt + 1));
                }
            }
        }

        private static object KeyDto(ResourceMappingResourceKey key) => new { id = key.Id, type = key.TypeId, variant = key.Variant, packageId = key.PackageId };
        private static string ComputeSha256(string path) { using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant(); }
        public sealed class GlobalResourceBuildPlan
        {
            public GlobalResourceMappingBuildInput Input { get; set; }
            public GlobalResourceBuildPlanCompilation Compilation { get; set; }
            public List<GlobalResourceResolvedEntry> Entries { get; } = new();
            public List<GlobalResourceResolvedEntry> ExternalEntries { get; } = new();
            public List<GlobalResourceResolvedEntry> ExcludedEntries { get; } = new();
            public List<GlobalResourceBuildBundle> Bundles { get; } = new();
            public GlobalResourceBuildReport Report { get; } = new();
            public bool HasErrors => Input == null || Input.HasErrors || Compilation == null || !Compilation.Plan.Success;
        }
        public sealed class GlobalResourceResolvedEntry { public GlobalResourceBuildPlanEntry Entry { get; set; } public GlobalResourceMappingV2ResolvedSource Source { get; set; } }
        public sealed class GlobalResourceBuildBundle { public GlobalResourceBuildPlanBundle Bundle { get; set; } public List<GlobalResourceResolvedEntry> Entries { get; } = new(); }
        public sealed class GlobalResourceBuildReport
        {
            public string ProfileId { get; set; } = string.Empty; public string CatalogId { get; set; } = string.Empty; public string PackageId { get; set; } = string.Empty; public string BuildTarget { get; set; } = string.Empty;
            public int InternalEntryCount { get; set; } public int ExternalEntryCount { get; set; } public int ExcludedEntryCount { get; set; } public int SceneEntryCount { get; set; }
            public List<GlobalResourceBuildIssue> Issues { get; } = new();
        }
        public sealed class GlobalResourceBuildIssue { public string Severity { get; set; } = string.Empty; public string Code { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; public string SourcePath { get; set; } = string.Empty; public string ResourceKey { get; set; } = string.Empty; }
        private sealed class PublishedArtifact
        {
            public PublishedArtifact(string kind, string relativePath, string sha256) { Kind = kind; RelativePath = relativePath.Replace('\\', '/'); Sha256 = sha256; }
            public string Kind { get; }
            public string RelativePath { get; }
            public string Sha256 { get; }
        }
    }
}
