using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using MxFramework.Editor;
using MxFramework.ResourceMapping.Authoring;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace MxFramework.Tests.Resources
{
    public class GlobalPlayerResourceBuildProfileBuilderTests
    {
        [Test]
        public void ProductionBuildInput_UsesSharedCompilerAndResolvesUnitySources()
        {
            GlobalResourceMappingBuildInput input = GlobalResourceMappingBuilder.CreateBuildInput(GlobalResourceMappingBuilder.ProfilePath);
            Assert.False(input.HasErrors, GlobalResourceMappingBuilder.CreateReportText(input));
            Assert.AreEqual("production-v2", input.Mode);
            Assert.True(input.ProductionActive);
            Assert.True(input.WritesPlayerArtifacts);
            Assert.True(input.Compilation.Plan.Success);
            Assert.Greater(input.Compilation.Plan.Entries.Count, 0);
            Assert.AreEqual(
                input.Compilation.Plan.Entries.Count,
                input.ResolvedSources.Count,
                "Every current production plan entry must resolve to a Unity source.");
            StringAssert.StartsWith("sha256:", input.Compilation.Plan.MappingInputFingerprint);
            StringAssert.StartsWith("sha256:", input.Compilation.Plan.BuildPlanFingerprint);
            StringAssert.StartsWith("sha256:", input.SourceInputFingerprint);
        }

        [Test]
        public void ProductionBuildInput_DetectsReferencedUnitySourceDriftWithoutPlanChange()
        {
            string assetRoot = "Assets/__PackagingGlobalSourceProbe_" + Guid.NewGuid().ToString("N");
            string probePath = assetRoot + "/body_albedo.png";
            string tempRoot = Path.Combine("Temp", "PackagingGlobalSourceProbe", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(assetRoot);
                Directory.CreateDirectory(tempRoot);
                File.Copy("Assets/MxFrameworkGenerated/CharacterPackages/000002/resources/textures/body_albedo.png",
                    probePath);
                AssetDatabase.ImportAsset(probePath, ImportAssetOptions.ForceUpdate);
                JObject profile = JObject.Parse(File.ReadAllText(GlobalPlayerResourceBuildProfileBuilder.ProfilePath));
                JObject entry = ((JArray)profile["entries"]).OfType<JObject>().Single(item =>
                    item["resourceKey"] is JObject key
                    && (string)key["id"] == "char.000002.texture.body_albedo");
                entry["sourceLocator"]["cachedPath"] = probePath;
                string profilePath = Path.Combine(tempRoot, "profile.json");
                File.WriteAllText(profilePath, profile.ToString());
                GlobalResourceMappingBuildInput before = GlobalResourceMappingBuilder.CreateBuildInput(profilePath);
                Assert.IsFalse(before.HasErrors, GlobalResourceMappingBuilder.CreateReportText(before));

                File.Copy("Assets/MxFrameworkGenerated/CharacterPackages/000009/resources/textures/baked/BaseColor.png",
                    probePath, overwrite: true);
                AssetDatabase.ImportAsset(probePath, ImportAssetOptions.ForceUpdate);
                GlobalResourceMappingBuildInput after = GlobalResourceMappingBuilder.CreateBuildInput(profilePath);
                Assert.IsFalse(after.HasErrors, GlobalResourceMappingBuilder.CreateReportText(after));
                Assert.AreEqual(before.Compilation.Plan.BuildPlanFingerprint,
                    after.Compilation.Plan.BuildPlanFingerprint);
                Assert.AreNotEqual(before.SourceInputFingerprint, after.SourceInputFingerprint);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetRoot);
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void EditorCatalogAndPlayerPlan_HaveIdenticalInternalKeyAndBundleMembership()
        {
            GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan =
                GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            Assert.False(plan.HasErrors, GlobalPlayerResourceBuildProfileBuilder.CreateReportText(plan.Report));
            ResourceCatalog editorCatalog = GlobalPlayerResourceBuildProfileBuilder.CreateEditorAssetDatabaseCatalog(plan);
            int plannedInternal = plan.Compilation.Plan.Counts.Internal;
            Assert.AreEqual(plannedInternal, editorCatalog.Entries.Count);
            Assert.AreEqual(plannedInternal, plan.Bundles.Sum(bundle => bundle.Entries.Count));
            foreach (ResourceCatalogEntry entry in editorCatalog.Entries)
            {
                Assert.True(plan.Compilation.Plan.Entries.Any(candidate =>
                    candidate.ResourceKey != null && candidate.ResourceKey.Id == entry.Id &&
                    candidate.ResourceKey.TypeId == entry.TypeId && candidate.ResourceKey.PackageId == entry.PackageId));
            }
        }

        [Test]
        public void EditorCatalog_UsesConcreteEditorProviders()
        {
            GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan =
                GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            ResourceCatalog catalog = GlobalPlayerResourceBuildProfileBuilder.CreateEditorAssetDatabaseCatalog(plan);
            ResourceCatalogEntry scene = catalog.Entries.Single(entry => entry.Id == "scene.character_test");
            ResourceCatalogEntry texture = catalog.Entries.Single(entry => entry.Id == "ui.start_screen.button.normal");
            Assert.AreEqual(EditorSceneAssetDatabaseResourceProvider.Id, scene.ProviderId);
            Assert.AreEqual(EditorAssetDatabaseResourceProvider.Id, texture.ProviderId);
        }

        [Test]
        public void StrictProductionReader_RejectsRetiredSchema()
        {
            string root = Path.Combine("Temp", "MxFrameworkResourceMappingSchemaRejectionTests");
            string path = Path.Combine(root, "retired.json");
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{\"schemaVersion\":1,\"profileId\":\"retired\",\"entries\":[]}");
            try
            {
                GlobalResourceMappingBuildInput input = GlobalResourceMappingBuilder.CreateBuildInput(path);
                Assert.True(input.HasErrors);
                Assert.IsNull(input.Compilation);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void GeneratedPreloadGroups_UseCompiledPlanKeys()
        {
            GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan =
                GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            GeneratedResourcePreloadGroupCatalog groups = GlobalPlayerResourceBuildProfileBuilder.CreatePreloadGroupCatalog(plan);
            Assert.True(groups.TryGetPlan("SpawnCritical", out ResourcePreloadPlan preload));
            Assert.Greater(preload.ExplicitKeys.Count, 0);
        }

        [Test]
        public void BundleResidueLedger_RetainsFailedRetirementAcrossGenerationAndRestart()
        {
            using var fixture = new ResidueFixture();
            fixture.WriteBundle("X", "owned-X");
            fixture.WriteBundle("Y", "owned-Y");
            fixture.WriteMarker('a', "X", "Y");
            GlobalResourceBundleResidueLedger.Context ledger = fixture.PrepareFresh();
            Assert.IsTrue(ledger.AutoCleanupEnabled);
            fixture.Commit(ledger);

            fixture.WriteMarker('b', "Y");
            Assert.IsTrue(File.Exists(fixture.BundlePath("X")), "Uncommitted B must not clean X.");
            GlobalResourceBundleResidueLedger.Context recoveredAfterMarkerCommit = fixture.PrepareExisting();
            Assert.IsTrue(recoveredAfterMarkerCommit.AutoCleanupEnabled,
                "Restart must recover a committed B marker before retiring A-owned files.");
            Assert.IsTrue(File.Exists(fixture.BundlePath("X")), "B must commit before cleanup.");
            StringAssert.Contains("pending: eligible", fixture.Report());
            fixture.Cleanup(recoveredAfterMarkerCommit, _ => false);
            StringAssert.Contains("pending: failed", fixture.Report());
            StringAssert.Contains("counts.failed: 1", fixture.Report());

            GlobalResourceBundleResidueLedger.Context restarted = fixture.PrepareExisting();
            fixture.WriteMarker('c', "Y");
            fixture.Commit(restarted);
            StringAssert.Contains("pending: failed", fixture.Report());
            fixture.Cleanup(restarted, fixture.DeleteBundle);
            Assert.IsFalse(File.Exists(fixture.BundlePath("X")));
            Assert.IsTrue(File.Exists(fixture.BundlePath("Y")));
            Assert.IsFalse(fixture.Report().Contains("pending: ", StringComparison.Ordinal));
            StringAssert.Contains("counts.cleaned: 1", fixture.Report());
            StringAssert.Contains("\"disposition\":\"cleaned\"", File.ReadAllText(fixture.LedgerPath + ".receipts.jsonl"));
        }

        [Test]
        public void BundleResidueLedger_ProtectsReReferenceReplacementAndUnknownFiles()
        {
            using var fixture = new ResidueFixture();
            fixture.WriteBundle("X", "owned-X");
            fixture.WriteBundle("Y", "owned-Y");
            fixture.WriteBundle("manual", "unknown");
            fixture.WriteOtherTargetBundle("foreign");
            fixture.WriteMarker('a', "X", "Y");
            GlobalResourceBundleResidueLedger.Context ledger = fixture.PrepareFresh();
            fixture.Commit(ledger);
            fixture.WriteMarker('b', "Y");
            fixture.Commit(ledger);
            fixture.WriteBundle("X", "replaced-X");
            fixture.Cleanup(ledger, fixture.DeleteBundle);
            StringAssert.Contains("pending: blockedHashMismatch", fixture.Report());
            Assert.IsTrue(File.Exists(fixture.BundlePath("X")));

            fixture.WriteMarker('c', "X", "Y");
            fixture.Commit(ledger);
            fixture.Cleanup(ledger, fixture.DeleteBundle);
            Assert.IsTrue(File.Exists(fixture.BundlePath("X")));
            Assert.IsTrue(File.Exists(fixture.BundlePath("manual")));
            StringAssert.Contains("file: unknown", fixture.Report());
            StringAssert.Contains("file: otherTarget", fixture.Report());
            StringAssert.Contains("\"disposition\":\"reReferenced\"", File.ReadAllText(fixture.LedgerPath + ".receipts.jsonl"));

            fixture.WriteBundle("other-owned", "other profile");
            fixture.WriteMarkerForProfile('d', "q", "other-owned");
            fixture.Commit(ledger);
            StringAssert.Contains("file: otherProfile", fixture.Report());

            File.AppendAllText(fixture.LedgerPath, "corrupt");
            GlobalResourceBundleResidueLedger.Context corrupted = fixture.PrepareExisting();
            Assert.IsFalse(corrupted.AutoCleanupEnabled);
            Assert.IsNotEmpty(corrupted.Issue);
            fixture.Cleanup(corrupted, fixture.DeleteBundle);
            Assert.IsTrue(File.Exists(fixture.BundlePath("manual")));
        }

        [Test]
        public void BundleResidueLedger_MigrationInventoriesMarkerButNeverClaimsUnknownFile()
        {
            using var fixture = new ResidueFixture();
            fixture.WriteBundle("X", "owned-X");
            fixture.WriteBundle("Y", "owned-Y");
            fixture.WriteBundle("manual", "unknown");
            fixture.WriteMarker('a', "X", "Y");
            GlobalResourceBundleResidueLedger.Context migrated = fixture.PrepareExisting();
            Assert.IsFalse(migrated.AutoCleanupEnabled);
            StringAssert.Contains("file: unknown", fixture.Report());

            fixture.WriteMarker('b', "Y");
            fixture.Commit(migrated);
            Assert.IsTrue(migrated.AutoCleanupEnabled,
                "A committed new generation can retire files proven by the inventoried prior marker.");
            fixture.Cleanup(migrated, fixture.DeleteBundle);
            Assert.IsFalse(File.Exists(fixture.BundlePath("X")));
            Assert.IsTrue(File.Exists(fixture.BundlePath("manual")));

            fixture.WriteBundle("Y", "damaged-current-generation");
            GlobalResourceBundleResidueLedger.Context partial = fixture.PrepareExisting();
            Assert.IsFalse(partial.AutoCleanupEnabled);
            StringAssert.Contains("could not be verified", partial.Issue);
            fixture.Cleanup(partial, fixture.DeleteBundle);
            Assert.IsTrue(File.Exists(fixture.BundlePath("manual")));
        }

        [Test]
        public void BundleResidueLedger_UnityCleanupRemovesOwnedBundleAndMeta()
        {
            using var fixture = new ResidueFixture(useUnityAssets: true);
            fixture.WriteBundle("X.assetbundle", "owned-X");
            fixture.WriteBundle("Y.assetbundle", "owned-Y");
            AssetDatabase.ImportAsset(fixture.BundlePath("X.assetbundle").Replace('\\', '/'),
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(fixture.BundlePath("Y.assetbundle").Replace('\\', '/'),
                ImportAssetOptions.ForceUpdate);
            Assert.IsTrue(File.Exists(fixture.BundlePath("X.assetbundle") + ".meta"));
            fixture.WriteMarker('a', "X.assetbundle", "Y.assetbundle");
            GlobalResourceBundleResidueLedger.Context ledger = fixture.PrepareFresh();
            fixture.Commit(ledger);
            fixture.WriteMarker('b', "Y.assetbundle");
            fixture.Commit(ledger);
            fixture.CleanupWithUnity(ledger);
            Assert.IsFalse(File.Exists(fixture.BundlePath("X.assetbundle")));
            Assert.IsFalse(File.Exists(fixture.BundlePath("X.assetbundle") + ".meta"));
            Assert.IsTrue(File.Exists(fixture.BundlePath("Y.assetbundle")));
        }

        private sealed class ResidueFixture : IDisposable
        {
            private const string Target = "StandaloneWindows64";
            private readonly string _root = Path.Combine("Temp", "MxFrameworkResidueLedgerTests", Guid.NewGuid().ToString("N"));
            private readonly string _artifactRoot;
            private readonly string _unityAssetRoot;
            private readonly string _markerPath;
            public readonly string LedgerPath;

            public ResidueFixture(bool useUnityAssets = false)
            {
                _unityAssetRoot = useUnityAssets
                    ? "Assets/__BundleResidueCleanupFixture_" + Guid.NewGuid().ToString("N") : null;
                _artifactRoot = useUnityAssets
                    ? Path.Combine(_unityAssetRoot, "StreamingAssets")
                    : Path.Combine(_root, "StreamingAssets");
                _markerPath = Path.Combine(_root, "set.json");
                LedgerPath = Path.Combine(_root, "ledger.json");
                Directory.CreateDirectory(_root);
                Directory.CreateDirectory(_artifactRoot);
                foreach (string name in new[] { "catalog.json", "mounts.json", "preload.json", "dependencies.json" })
                    File.WriteAllText(Path.Combine(_artifactRoot, name), "{}\n");
            }

            public string BundlePath(string name) => Path.Combine(_artifactRoot,
                "MxFramework", "Resources", "Bundles", Target, name);

            public void WriteBundle(string name, string content)
            {
                string path = BundlePath(name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, content);
            }

            public void WriteOtherTargetBundle(string name)
            {
                string path = Path.Combine(_artifactRoot, "MxFramework", "Resources", "Bundles", "Android", name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "other");
            }

            public void WriteMarker(char generation, params string[] bundleNames) =>
                WriteMarkerForProfile(generation, "p", bundleNames);

            public void WriteMarkerForProfile(char generation, string profileId, params string[] bundleNames)
            {
                var artifacts = new List<string>();
                string[] names = { "catalog.json", "mounts.json", "preload.json", "dependencies.json" };
                string[] kinds = { "catalog", "catalogMounts", "preloadGroups", "bundleDependencies" };
                for (int i = 0; i < names.Length; i++)
                    artifacts.Add("{\"kind\":\"" + kinds[i] + "\",\"relativePath\":\"" + names[i]
                        + "\",\"sha256\":\"" + Hash(Path.Combine(_artifactRoot, names[i])) + "\"}");
                foreach (string name in bundleNames)
                {
                    string relative = "MxFramework/Resources/Bundles/" + Target + "/" + name;
                    artifacts.Add("{\"kind\":\"bundle\",\"relativePath\":\"" + relative
                        + "\",\"sha256\":\"" + Hash(BundlePath(name)) + "\"}");
                }
                string fingerprint = "sha256:" + new string(generation, 64);
                File.WriteAllText(_markerPath,
                    "{\"schemaVersion\":1,\"profileId\":\"" + profileId
                    + "\",\"catalogId\":\"c\",\"buildTarget\":\""
                    + Target + "\",\"mappingInputFingerprint\":\"" + fingerprint
                    + "\",\"buildPlanFingerprint\":\"" + fingerprint
                    + "\",\"sourceInputFingerprint\":\"" + fingerprint
                    + "\",\"artifacts\":[" + string.Join(",", artifacts) + "]}");
            }

            public GlobalResourceBundleResidueLedger.Context PrepareFresh() =>
                GlobalResourceBundleResidueLedger.Prepare("p", Target, Path.Combine(_root, "missing.json"), _artifactRoot, LedgerPath);

            public GlobalResourceBundleResidueLedger.Context PrepareExisting() =>
                GlobalResourceBundleResidueLedger.Prepare("p", Target, _markerPath, _artifactRoot, LedgerPath);

            public void Commit(GlobalResourceBundleResidueLedger.Context context) =>
                GlobalResourceBundleResidueLedger.RecordCommittedMarker(context, _markerPath, _artifactRoot);

            public void Cleanup(GlobalResourceBundleResidueLedger.Context context, Func<string, bool> delete) =>
                GlobalResourceBundleResidueLedger.CleanupCommitted(context, _markerPath, _artifactRoot, delete);

            public void CleanupWithUnity(GlobalResourceBundleResidueLedger.Context context) =>
                GlobalResourceBundleResidueLedger.CleanupCommitted(context, _markerPath, _artifactRoot);

            public bool DeleteBundle(string fullPath)
            {
                File.Delete(fullPath);
                return true;
            }

            public string Report() => GlobalResourceBundleResidueLedger.CreateReport(
                "p", Target, _markerPath, _artifactRoot, LedgerPath);

            public void Dispose()
            {
                if (_unityAssetRoot != null) AssetDatabase.DeleteAsset(_unityAssetRoot);
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }

            private static string Hash(string path)
            {
                using SHA256 sha = SHA256.Create();
                using FileStream stream = File.OpenRead(path);
                return "sha256:" + BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
