using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class RuntimeModPackagePatchMergerTests
    {
        [Test]
        public void RuntimeModPackagePatchMerger_SinglePackage_MatchesSingleLoad()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackage(container, "pkg.single", "Preview", ModifierUpsert(200001, 80), BuffUpsert(100001, 8f, 200001));
                RuntimeModPackageLoadPlan plan = BuildPlan(container);
                IConfigProvider baseRegistry = CreateBaseRegistry();

                RuntimeModPackageMergeResult result = RuntimeModPackagePatchMerger.Merge(plan, baseRegistry);

                Assert.IsTrue(result.Success);
                Assert.IsTrue(result.Registry.TryGetConfig<BasicBuffConfig>(100001, out BasicBuffConfig buff));
                Assert.IsTrue(result.Registry.TryGetConfig<BasicModifierConfig>(200001, out BasicModifierConfig modifier));
                Assert.AreEqual(8f, buff.Duration);
                Assert.AreEqual(80, modifier.Parameters[0]);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackagePatchMerger_MultiplePackages_DifferentIds_AllApplied()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackage(container, "pkg.a", "Preview", ModifierUpsert(200002, 20), BuffUpsert(100002, 12f, 200002));
                CreatePackage(container, "pkg.b", "Mod", ModifierUpsert(200003, 30), BuffUpsert(100003, 15f, 200003));

                RuntimeModPackageMergeResult result = RuntimeModPackagePatchMerger.Merge(BuildPlan(container), CreateBaseRegistry());

                Assert.IsTrue(result.Success);
                Assert.IsTrue(result.Registry.ContainsConfig<BasicBuffConfig>(100002));
                Assert.IsTrue(result.Registry.ContainsConfig<BasicBuffConfig>(100003));
                Assert.IsTrue(result.Registry.ContainsConfig<BasicModifierConfig>(200002));
                Assert.IsTrue(result.Registry.ContainsConfig<BasicModifierConfig>(200003));
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackagePatchMerger_MultiplePackages_SameId_LastWins()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackage(container, "pkg.a", "Preview", ModifierUpsert(200001, 70), BuffUpsert(100001, 7f, 200001));
                CreatePackage(container, "pkg.z", "Mod", ModifierUpsert(200001, 95), BuffUpsert(100001, 9f, 200001));

                RuntimeModPackageMergeResult result = RuntimeModPackagePatchMerger.Merge(BuildPlan(container), CreateBaseRegistry());

                Assert.IsTrue(result.Success);
                Assert.IsTrue(result.Registry.TryGetConfig<BasicModifierConfig>(200001, out BasicModifierConfig modifier));
                Assert.IsTrue(result.Registry.TryGetConfig<BasicBuffConfig>(100001, out BasicBuffConfig buff));
                Assert.AreEqual(95, modifier.Parameters[0]);
                Assert.AreEqual(9f, buff.Duration);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackagePatchMerger_MultiplePackages_SameId_RecordsOverride()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackage(container, "pkg.a", "Preview", ModifierUpsert(200001, 70));
                CreatePackage(container, "pkg.z", "Mod", ModifierUpsert(200001, 95));

                RuntimeModPackageMergeResult result = RuntimeModPackagePatchMerger.Merge(BuildPlan(container), CreateBaseRegistry());

                Assert.IsTrue(result.Success);
                Assert.AreEqual(1, result.Report.Overrides.Count);
                Assert.AreEqual("pkg.z", result.Report.Overrides[0].WinnerPackageId);
                CollectionAssert.AreEqual(new[] { "pkg.a", "pkg.z" }, result.Report.Overrides[0].PackageChain);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackagePatchMerger_RemoveExisting_RemovesRow()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackage(container, "pkg.remove", "Preview", BuffRemove(100001));
                RuntimeModPackageMergeResult result = RuntimeModPackagePatchMerger.Merge(BuildPlan(container), CreateBaseRegistry());

                Assert.IsTrue(result.Success);
                Assert.IsFalse(result.Registry.ContainsConfig<BasicBuffConfig>(100001));
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackagePatchMerger_RemoveMissing_RecordsNoop()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackage(container, "pkg.remove", "Preview", BuffRemove(199999));
                RuntimeModPackageMergeResult result = RuntimeModPackagePatchMerger.Merge(BuildPlan(container), CreateBaseRegistry());

                Assert.IsTrue(result.Success);
                Assert.AreEqual(ConfigMergeChangeKind.Noop, result.ChangeSet.Changes[0].ChangeKind);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackagePatchMerger_SkippedPackages_NotAppliedButReported()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackage(container, "pkg.a", "Preview", ModifierUpsert(200002, 20));
                CreatePackage(container, "pkg.b", "Preview", ModifierUpsert(200003, 30));

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                var enabled = new HashSet<string> { "pkg.a" };
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog, enabled);
                RuntimeModPackageMergeResult result = RuntimeModPackagePatchMerger.Merge(plan, CreateBaseRegistry());

                Assert.IsTrue(result.Success);
                Assert.AreEqual(1, result.Report.SkippedPackageCount);
                Assert.AreEqual("pkg.b", result.Report.Packages[0].PackageId);
                Assert.IsTrue(result.Report.Packages[0].IsSkipped);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackagePatchMerger_OrderedPackageLoadFails_ReturnsError()
        {
            string container = CreateTempContainer();
            try
            {
                string brokenPath = CreatePackage(container, "pkg.broken", "Preview", ModifierUpsert(200002, 20));
                RuntimeModPackageLoadPlan plan = BuildPlan(container);
                File.Delete(Path.Combine(brokenPath, "runtime/patch.json"));

                RuntimeModPackageMergeResult result = RuntimeModPackagePatchMerger.Merge(plan, CreateBaseRegistry());

                Assert.IsFalse(result.Success);
                Assert.IsNotEmpty(result.Report.Errors);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        private static RuntimeModPackageLoadPlan BuildPlan(string container)
        {
            RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
            return RuntimeModPackageLoadPlanBuilder.Build(catalog);
        }

        private static IConfigProvider CreateBaseRegistry()
        {
            var registry = new ConfigRegistry();

            var modTable = new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
            modTable.Add(new BasicModifierConfig(200001, new LocalizedTextKey("mod.200001.name"), new LocalizedTextKey("mod.200001.desc"), 2, new[] { 50 }));
            registry.RegisterProvider<BasicModifierConfig>(modTable);

            var buffTable = new ConfigTable<BasicBuffConfig>(BasicBuffConfig.CreateSchema());
            buffTable.Add(new BasicBuffConfig(100001, new LocalizedTextKey("buff.100001.name"), new LocalizedTextKey("buff.100001.desc"), 5f, 3, modifierId: 200001));
            registry.RegisterProvider<BasicBuffConfig>(buffTable);

            return registry;
        }

        private static string CreateTempContainer()
        {
            string path = Path.Combine(Path.GetTempPath(), "mx_merge_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string CreatePackage(string container, string packageId, string kind, params string[] patchEntries)
        {
            string dir = Directory.CreateDirectory(Path.Combine(container, packageId.Replace(".", "_"))).FullName;
            string runtimeDir = Directory.CreateDirectory(Path.Combine(dir, "runtime")).FullName;

            File.WriteAllText(Path.Combine(dir, "mod.json"),
                "{\"schemaVersion\":1,\"packageId\":\"" + packageId + "\",\"displayName\":\"Test\",\"author\":\"Test\",\"version\":\"0.1.0\",\"kind\":\"" + kind + "\",\"runtimePatch\":\"runtime/patch.json\"}");

            string layer = kind == "Mod" ? "Mod" : "Patch";
            string patchJson = "{\"format\":\"mx.runtimeConfigPatch.v1\",\"sourceId\":\"" + packageId + "\",\"layer\":\"" + layer + "\",\"modifiers\":["
                + string.Join(",", GetEntriesByType(patchEntries, "modifier"))
                + "],\"buffs\":["
                + string.Join(",", GetEntriesByType(patchEntries, "buff"))
                + "]}";
            File.WriteAllText(Path.Combine(runtimeDir, "patch.json"), patchJson);
            return dir;
        }

        private static IEnumerable<string> GetEntriesByType(IEnumerable<string> entries, string type)
        {
            foreach (string entry in entries)
            {
                if (entry.StartsWith(type + ":", StringComparison.Ordinal))
                    yield return entry.Substring(type.Length + 1);
            }
        }

        private static string ModifierUpsert(int id, int value)
        {
            return "modifier:{\"operation\":\"Upsert\",\"id\":" + id + ",\"nameText\":\"mod." + id + ".name\",\"descriptionText\":\"mod." + id + ".desc\",\"paramIndex\":2,\"parameters\":[" + value + "]}";
        }

        private static string BuffUpsert(int id, float duration, int modifierId)
        {
            return "buff:{\"operation\":\"Upsert\",\"id\":" + id + ",\"nameText\":\"buff." + id + ".name\",\"descriptionText\":\"buff." + id + ".desc\",\"duration\":" + duration + ",\"maxLayers\":1,\"modifierId\":" + modifierId + "}";
        }

        private static string BuffRemove(int id)
        {
            return "buff:{\"operation\":\"Remove\",\"id\":" + id + "}";
        }
    }
}
