using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class RuntimeModDiagnosticSnapshotTests
    {
        [Test]
        public void RuntimeModDiagnosticSnapshotBuilder_SuccessInput_ProducesSuccessSnapshot()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackage(container, "pkg.a", "Preview", ModifierUpsert(200001, 70));
                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog);
                RuntimeModPackageMergeResult mergeResult = RuntimeModPackagePatchMerger.Merge(plan, CreateBaseRegistry());

                RuntimeModDiagnosticSnapshot snapshot = RuntimeModDiagnosticSnapshotBuilder.Build(catalog, null, plan, mergeResult);

                Assert.AreEqual(RuntimeModDiagnosticSnapshot.ExpectedFormat, snapshot.Format);
                Assert.IsTrue(snapshot.Success);
                Assert.AreEqual(1, snapshot.Summary.Discovered);
                Assert.AreEqual(1, snapshot.Summary.Ordered);
                Assert.AreEqual(0, snapshot.Summary.Errors);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModDiagnosticSnapshotBuilder_LoadoutMissingKey_ProducesWarning()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackage(container, "pkg.a", "Preview", ModifierUpsert(200001, 70));
                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                var loadout = new RuntimeModPackageLoadout("demo", new[] { "missing|pkg" });
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog, loadout);
                RuntimeModPackageMergeResult mergeResult = RuntimeModPackagePatchMerger.Merge(plan, CreateBaseRegistry());

                RuntimeModDiagnosticSnapshot snapshot = RuntimeModDiagnosticSnapshotBuilder.Build(catalog, loadout, plan, mergeResult);

                Assert.IsTrue(snapshot.Success);
                Assert.GreaterOrEqual(snapshot.Warnings.Count, 1);
                Assert.IsTrue(snapshot.Warnings[0].Message.Contains("missing package key"));
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModDiagnosticSnapshotBuilder_MergeError_ProducesErrorAndFailure()
        {
            string container = CreateTempContainer();
            try
            {
                string packagePath = CreatePackage(container, "pkg.broken", "Preview", ModifierUpsert(200001, 70));
                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog);
                File.Delete(Path.Combine(packagePath, "runtime/patch.json"));
                RuntimeModPackageMergeResult mergeResult = RuntimeModPackagePatchMerger.Merge(plan, CreateBaseRegistry());

                RuntimeModDiagnosticSnapshot snapshot = RuntimeModDiagnosticSnapshotBuilder.Build(catalog, null, plan, mergeResult);

                Assert.IsFalse(snapshot.Success);
                Assert.GreaterOrEqual(snapshot.Errors.Count, 1);
                Assert.AreEqual("MergeError", snapshot.Errors[0].Code);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModDiagnosticSnapshotBuilder_OrdersPackagesByPackageKey()
        {
            string container = CreateTempContainer();
            try
            {
                CreatePackageAt(container, "z_dir", "pkg.z", "Preview", ModifierUpsert(200001, 70));
                CreatePackageAt(container, "a_dir", "pkg.a", "Preview", ModifierUpsert(200002, 90));
                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog);
                RuntimeModPackageMergeResult mergeResult = RuntimeModPackagePatchMerger.Merge(plan, CreateBaseRegistry());

                RuntimeModDiagnosticSnapshot snapshot = RuntimeModDiagnosticSnapshotBuilder.Build(catalog, null, plan, mergeResult);

                Assert.AreEqual(2, snapshot.Packages.Count);
                string p0 = snapshot.Packages[0].PackageKey;
                string p1 = snapshot.Packages[1].PackageKey;
                Assert.LessOrEqual(string.Compare(p0, p1, StringComparison.Ordinal), 0);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModDiagnosticSnapshotJson_Save_UsesCamelCase()
        {
            var snapshot = new RuntimeModDiagnosticSnapshot
            {
                Format = RuntimeModDiagnosticSnapshot.ExpectedFormat,
                GeneratedUtc = DateTime.UtcNow.ToString("O"),
                Success = true
            };

            string json = RuntimeModDiagnosticSnapshotJson.SaveToJson(snapshot);
            Assert.IsTrue(json.Contains("\"generatedUtc\""));
            Assert.IsTrue(json.Contains("\"success\""));
            Assert.IsFalse(json.Contains("\"GeneratedUtc\""));
        }

        [Test]
        public void RuntimeModDiagnosticSnapshotJson_SaveThenLoad_RoundTrips()
        {
            var snapshot = new RuntimeModDiagnosticSnapshot
            {
                Format = RuntimeModDiagnosticSnapshot.ExpectedFormat,
                GeneratedUtc = "2026-05-07T00:00:00.0000000Z",
                Success = true,
                Summary = new RuntimeModDiagnosticSummary { Discovered = 2, Ordered = 1 }
            };

            string json = RuntimeModDiagnosticSnapshotJson.SaveToJson(snapshot);
            RuntimeModDiagnosticSnapshot loaded = RuntimeModDiagnosticSnapshotJson.LoadFromJson(json);

            Assert.AreEqual(RuntimeModDiagnosticSnapshot.ExpectedFormat, loaded.Format);
            Assert.AreEqual("2026-05-07T00:00:00.0000000Z", loaded.GeneratedUtc);
            Assert.IsTrue(loaded.Success);
            Assert.AreEqual(2, loaded.Summary.Discovered);
            Assert.AreEqual(1, loaded.Summary.Ordered);
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
            string path = Path.Combine(Path.GetTempPath(), "mx_diag_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string CreatePackage(string container, string packageId, string kind, params string[] patchEntries)
        {
            string dirName = packageId.Replace(".", "_");
            return CreatePackageAt(container, dirName, packageId, kind, patchEntries);
        }

        private static string CreatePackageAt(string container, string dirName, string packageId, string kind, params string[] patchEntries)
        {
            string dir = Directory.CreateDirectory(Path.Combine(container, dirName)).FullName;
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
    }
}
