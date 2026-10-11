using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Runtime;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class ModPackageCatalogTests
    {
        [Test]
        public void RuntimeModPackageDiscovery_MissingContainer_ReturnsEmptyCatalog()
        {
            string nonexistent = Path.Combine(Path.GetTempPath(), "mx_test_" + Guid.NewGuid().ToString("N"));

            RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { nonexistent });

            Assert.IsNotNull(catalog);
            Assert.AreEqual(0, catalog.Items.Count);
        }

        [Test]
        public void RuntimeModPackageDiscovery_IgnoresChildWithoutManifest()
        {
            string container = CreateTempContainer();
            try
            {
                // Create a subdirectory WITHOUT mod.json
                Directory.CreateDirectory(Path.Combine(container, "no-manifest"));

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });

                Assert.IsNotNull(catalog);
                Assert.AreEqual(0, catalog.Items.Count);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageDiscovery_ValidPackage_ProducesValidItem()
        {
            string container = CreateTempContainer();
            try
            {
                CreateValidPreviewPackage(container, "test.valid");

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });

                Assert.AreEqual(1, catalog.Items.Count);
                Assert.IsTrue(catalog.Items[0].IsValid);
                Assert.AreEqual("test.valid", catalog.Items[0].Manifest?.PackageId);
                Assert.AreEqual("Preview", catalog.Items[0].Manifest?.Kind);
                Assert.AreEqual("test.valid|test_valid", catalog.Items[0].PackageKey);
                Assert.AreEqual("test_valid", catalog.Items[0].PackageRelativePath);
                Assert.AreEqual(0, catalog.Items[0].Errors.Count);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageDiscovery_InvalidPackage_ProducesInvalidItemWithErrors()
        {
            string container = CreateTempContainer();
            try
            {
                // Create a package directory with mod.json that has no runtimePatch
                string pkgDir = Directory.CreateDirectory(Path.Combine(container, "bad-pkg")).FullName;
                File.WriteAllText(Path.Combine(pkgDir, "mod.json"),
                    "{\"schemaVersion\":1,\"packageId\":\"test.invalid\",\"kind\":\"Preview\"}");

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });

                Assert.AreEqual(1, catalog.Items.Count);
                Assert.IsFalse(catalog.Items[0].IsValid);
                Assert.IsTrue(catalog.Items[0].Errors.Count > 0);
                Assert.IsTrue(catalog.Items[0].Errors[0].Contains("runtimePatch"));
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoadPlan_NullEnabled_UsesAllValidPackages()
        {
            string container = CreateTempContainer();
            try
            {
                CreateValidPreviewPackage(container, "pkg.a");
                CreateValidPreviewPackage(container, "pkg.b");

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog, enabledPackageIds: null);

                Assert.AreEqual(2, plan.OrderedItems.Count);
                Assert.AreEqual(0, plan.SkippedItems.Count);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoadPlan_EnabledSet_FiltersPackages()
        {
            string container = CreateTempContainer();
            try
            {
                CreateValidPreviewPackage(container, "pkg.a");
                CreateValidPreviewPackage(container, "pkg.b");
                CreateValidPreviewPackage(container, "pkg.c");

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                var enabled = new HashSet<string> { "pkg.a", "pkg.c" };
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog, enabled);

                Assert.AreEqual(2, plan.OrderedItems.Count);
                Assert.AreEqual(1, plan.SkippedItems.Count);
                Assert.AreEqual("pkg.b", plan.SkippedItems[0].Manifest?.PackageId);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoadPlan_InvalidPackage_AlwaysSkipped()
        {
            string container = CreateTempContainer();
            try
            {
                CreateValidPreviewPackage(container, "valid.pkg");
                // Create invalid package
                string pkgDir = Directory.CreateDirectory(Path.Combine(container, "bad-pkg")).FullName;
                File.WriteAllText(Path.Combine(pkgDir, "mod.json"),
                    "{\"schemaVersion\":1,\"packageId\":\"bad.pkg\",\"kind\":\"Preview\"}");

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                var enabled = new HashSet<string> { "valid.pkg", "bad.pkg" };
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog, enabled);

                Assert.AreEqual(1, plan.OrderedItems.Count);
                Assert.AreEqual("valid.pkg", plan.OrderedItems[0].Manifest?.PackageId);
                Assert.AreEqual(1, plan.SkippedItems.Count);
                Assert.AreEqual("bad.pkg", plan.SkippedItems[0].Manifest?.PackageId);
                Assert.IsFalse(plan.SkippedItems[0].IsValid);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoadPlan_SortsPreviewBeforeMod()
        {
            string container = CreateTempContainer();
            try
            {
                CreateValidPreviewPackage(container, "z.pkg"); // Preview
                CreateValidModPackage(container, "a.mod");    // Mod

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog);

                Assert.AreEqual(2, plan.OrderedItems.Count);
                // Preview should come before Mod, even though packageId sorts differently
                Assert.AreEqual("Preview", plan.OrderedItems[0].Manifest?.Kind);
                Assert.AreEqual("z.pkg", plan.OrderedItems[0].Manifest?.PackageId);
                Assert.AreEqual("Mod", plan.OrderedItems[1].Manifest?.Kind);
                Assert.AreEqual("a.mod", plan.OrderedItems[1].Manifest?.PackageId);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoadPlan_DuplicatePackageId_WarnsAndSortsByRootPath()
        {
            string container = CreateTempContainer();
            try
            {
                // Two packages with same packageId, different root paths
                CreateValidPreviewPackage(container, "dup.pkg");
                string secondDir = Directory.CreateDirectory(Path.Combine(container, "dup-copy")).FullName;
                CreateValidPreviewPackageAt(secondDir, "dup.pkg");

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog);

                Assert.AreEqual(2, plan.OrderedItems.Count);
                // Both should be in ordered list (not blocked)
                // At least one should have a warning about duplicate
                bool hasWarning = plan.OrderedItems[0].Warnings.Count > 0 ||
                                  plan.OrderedItems[1].Warnings.Count > 0;
                Assert.IsTrue(hasWarning, "Duplicate packageId should produce a warning");
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoadoutJson_Roundtrip_PreservesProfileAndKeys()
        {
            var loadout = new RuntimeModPackageLoadout(
                profileId: "demo",
                enabledPackageKeys: new[] { "a|pkg-a", "b|pkg-b" },
                displayName: "Demo");

            string json = RuntimeModPackageLoadoutJson.SaveToJson(loadout);
            RuntimeModPackageLoadout parsed = RuntimeModPackageLoadoutJson.LoadFromJson(json);

            Assert.AreEqual(RuntimeModPackageLoadout.ExpectedFormat, parsed.Format);
            Assert.AreEqual("demo", parsed.ProfileId);
            Assert.AreEqual(2, parsed.EnabledPackageKeys.Count);
            Assert.AreEqual("a|pkg-a", parsed.EnabledPackageKeys[0]);
            Assert.AreEqual("b|pkg-b", parsed.EnabledPackageKeys[1]);
        }

        [Test]
        public void RuntimeModPackageLoadPlan_FromLoadout_UsesPackageKeysAndWarnings()
        {
            string container = CreateTempContainer();
            try
            {
                CreateValidPreviewPackage(container, "pkg.a");
                CreateValidPreviewPackage(container, "pkg.b");

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                string keyA = FindKey(catalog, "pkg.a");
                var loadout = new RuntimeModPackageLoadout(
                    profileId: "demo",
                    enabledPackageKeys: new[] { keyA, "missing|path" });

                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog, loadout);

                Assert.AreEqual(1, plan.OrderedItems.Count);
                Assert.AreEqual("pkg.a", plan.OrderedItems[0].Manifest?.PackageId);
                Assert.AreEqual(1, plan.Warnings.Count);
                Assert.IsTrue(plan.Warnings[0].Contains("missing package key"));
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoader_OldPackageWithoutResourceCatalog_RemainsValid()
        {
            string container = CreateTempContainer();
            try
            {
                string dir = Directory.CreateDirectory(Path.Combine(container, "old-package")).FullName;
                CreateValidPreviewPackageAt(dir, "pkg.old");

                RuntimeModPackageLoadResult result = RuntimeModPackageLoader.LoadFromDirectory(dir);

                Assert.IsFalse(result.HasResourceCatalog);
                Assert.AreEqual(string.Empty, result.Manifest.ResourceCatalog);
                Assert.AreEqual(string.Empty, result.ResourceCatalogFilePath);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoader_ResourceCatalog_ResolvesPath()
        {
            string container = CreateTempContainer();
            try
            {
                string dir = CreateValidPreviewPackageWithResourceCatalog(container, "pkg.resources");

                RuntimeModPackageLoadResult result = RuntimeModPackageLoader.LoadFromDirectory(dir);

                Assert.IsTrue(result.HasResourceCatalog);
                Assert.IsTrue(File.Exists(result.ResourceCatalogFilePath));
                Assert.AreEqual("resources/catalog.json", result.Manifest.ResourceCatalog);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoader_ResourceCatalogPathTraversal_Fails()
        {
            string container = CreateTempContainer();
            try
            {
                string dir = Directory.CreateDirectory(Path.Combine(container, "bad-resource")).FullName;
                CreateValidPreviewPackageAt(dir, "pkg.bad.resource", resourceCatalog: "../catalog.json");

                RuntimeModPackageLoadException ex = Assert.Throws<RuntimeModPackageLoadException>(() => RuntimeModPackageLoader.LoadFromDirectory(dir));

                Assert.IsTrue(ex.Message.Contains("resourceCatalog"));
                Assert.IsTrue(ex.Message.Contains("outside"));
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageLoader_ResourceCatalogMissingFile_Fails()
        {
            string container = CreateTempContainer();
            try
            {
                string dir = Directory.CreateDirectory(Path.Combine(container, "missing-resource")).FullName;
                CreateValidPreviewPackageAt(dir, "pkg.missing.resource", resourceCatalog: "resources/missing.json");

                RuntimeModPackageLoadException ex = Assert.Throws<RuntimeModPackageLoadException>(() => RuntimeModPackageLoader.LoadFromDirectory(dir));

                Assert.IsTrue(ex.Message.Contains("resourceCatalog"));
                Assert.IsTrue(ex.Message.Contains("not found"));
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageResourceCatalog_CanBeMountedWithOverrideAndPackageRoute()
        {
            string container = CreateTempContainer();
            try
            {
                string dir = CreateValidPreviewPackageWithResourceCatalog(container, "pkg.resources");
                RuntimeModPackageLoadResult loadResult = RuntimeModPackageLoader.LoadFromDirectory(dir);

                var provider = new MemoryResourceProvider()
                    .Register("base/title", "base")
                    .Register("mod/title", "mod");
                var manager = new ResourceManager();
                manager.RegisterProvider(provider);
                manager.AddCatalog(new ResourceCatalog(
                    "base.resources",
                    "base.package",
                    new[]
                    {
                        new ResourceCatalogEntry(
                            "demo.text.title",
                            ResourceTypeIds.String,
                            "memory",
                            "base/title")
                    }));

                ResourceCatalog modCatalog = StreamingResourceCatalogLoader.LoadFromFile(loadResult.ResourceCatalogFilePath);
                manager.AddCatalog(modCatalog);

                ResourceHandle<string> global = manager.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String)).Value;
                ResourceHandle<string> exactBase = manager.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String, packageId: "base.package")).Value;

                Assert.AreEqual("mod", global.Value);
                Assert.AreEqual("base", exactBase.Value);

                manager.Release(global);
                manager.Release(exactBase);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageResourceCatalog_ConflictWithoutAllowOverride_Throws()
        {
            string container = CreateTempContainer();
            try
            {
                string dir = CreateValidPreviewPackageWithResourceCatalog(
                    container,
                    "pkg.conflict",
                    allowOverride: false);
                RuntimeModPackageLoadResult loadResult = RuntimeModPackageLoader.LoadFromDirectory(dir);

                var manager = new ResourceManager();
                manager.RegisterProvider(new MemoryResourceProvider());
                manager.AddCatalog(new ResourceCatalog(
                    "base.resources",
                    "base.package",
                    new[]
                    {
                        new ResourceCatalogEntry(
                            "demo.text.title",
                            ResourceTypeIds.String,
                            "memory",
                            "base/title")
                    }));

                ResourceCatalog modCatalog = StreamingResourceCatalogLoader.LoadFromFile(loadResult.ResourceCatalogFilePath);
                ResourceCatalogException ex = Assert.Throws<ResourceCatalogException>(() => manager.AddCatalog(modCatalog));

                Assert.IsTrue(ex.Message.Contains("override"));
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageResourceCatalog_OverrideTypeMismatch_Throws()
        {
            string container = CreateTempContainer();
            try
            {
                string dir = CreateValidPreviewPackageWithResourceCatalog(
                    container,
                    "pkg.type_mismatch",
                    typeId: ResourceTypeIds.TextAsset,
                    allowOverride: true);
                RuntimeModPackageLoadResult loadResult = RuntimeModPackageLoader.LoadFromDirectory(dir);

                var manager = new ResourceManager();
                manager.RegisterProvider(new MemoryResourceProvider());
                manager.AddCatalog(new ResourceCatalog(
                    "base.resources",
                    "base.package",
                    new[]
                    {
                        new ResourceCatalogEntry(
                            "demo.text.title",
                            ResourceTypeIds.String,
                            "memory",
                            "base/title")
                    }));

                ResourceCatalog modCatalog = StreamingResourceCatalogLoader.LoadFromFile(loadResult.ResourceCatalogFilePath);
                ResourceCatalogException ex = Assert.Throws<ResourceCatalogException>(() => manager.AddCatalog(modCatalog));

                Assert.IsTrue(ex.Message.Contains("type mismatch"));
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        [Test]
        public void RuntimeModPackageResourceCatalog_DisabledPackage_IsNotMounted()
        {
            string container = CreateTempContainer();
            try
            {
                string enabledDir = CreateValidPreviewPackageWithResourceCatalog(
                    container,
                    "pkg.enabled",
                    address: "enabled/title");
                CreateValidPreviewPackageWithResourceCatalog(
                    container,
                    "pkg.disabled",
                    address: "disabled/title");

                RuntimeModPackageCatalog catalog = RuntimeModPackageDiscovery.Discover(new[] { container });
                var enabledPackageIds = new HashSet<string> { "pkg.enabled" };
                RuntimeModPackageLoadPlan plan = RuntimeModPackageLoadPlanBuilder.Build(catalog, enabledPackageIds);

                var manager = new ResourceManager();
                manager.RegisterProvider(new MemoryResourceProvider()
                    .Register("enabled/title", "enabled")
                    .Register("disabled/title", "disabled"));

                for (int i = 0; i < plan.OrderedItems.Count; i++)
                {
                    RuntimeModPackageLoadResult loadResult = RuntimeModPackageLoader.LoadFromDirectory(plan.OrderedItems[i].PackageRootPath);
                    ResourceCatalog packageCatalog = StreamingResourceCatalogLoader.LoadFromFile(loadResult.ResourceCatalogFilePath);
                    manager.AddCatalog(packageCatalog);
                }

                ResourceLoadResult<ResourceHandle<string>> enabledResult = manager.Load<string>(
                    new ResourceKey("demo.text.title", ResourceTypeIds.String, packageId: "pkg.enabled"));
                ResourceLoadResult<ResourceHandle<string>> disabledResult = manager.Load<string>(
                    new ResourceKey("demo.text.title", ResourceTypeIds.String, packageId: "pkg.disabled"));

                Assert.AreEqual(1, plan.OrderedItems.Count);
                Assert.AreEqual(1, plan.SkippedItems.Count);
                Assert.AreEqual(enabledDir, plan.OrderedItems[0].PackageRootPath);
                Assert.IsTrue(enabledResult.Success);
                Assert.AreEqual("enabled", enabledResult.Value.Value);
                Assert.IsFalse(disabledResult.Success);
                Assert.AreEqual(ResourceErrorCode.NotFound, disabledResult.Error.Code);

                manager.Release(enabledResult.Value);
            }
            finally
            {
                Directory.Delete(container, true);
            }
        }

        // ===== Helpers =====

        private static string CreateTempContainer()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mx_test_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        private static void CreateValidPreviewPackage(string container, string packageId)
        {
            string dir = Directory.CreateDirectory(Path.Combine(container, packageId.Replace(".", "_"))).FullName;
            CreateValidPreviewPackageAt(dir, packageId);
        }

        private static void CreateValidPreviewPackageAt(string dir, string packageId, string resourceCatalog = "")
        {
            string runtimeDir = Directory.CreateDirectory(Path.Combine(dir, "runtime")).FullName;
            string resourceCatalogJson = string.IsNullOrWhiteSpace(resourceCatalog)
                ? string.Empty
                : ",\"resourceCatalog\":\"" + resourceCatalog + "\"";
            File.WriteAllText(Path.Combine(dir, "mod.json"),
                "{\"schemaVersion\":1,\"packageId\":\"" + packageId + "\",\"displayName\":\"Test\",\"author\":\"Test\",\"version\":\"0.1.0\",\"kind\":\"Preview\",\"runtimePatch\":\"runtime/patch.json\"" + resourceCatalogJson + "}");
            File.WriteAllText(Path.Combine(runtimeDir, "patch.json"),
                "{\"format\":\"mx.runtimeConfigPatch.v1\",\"sourceId\":\"" + packageId + "\",\"layer\":\"Patch\",\"modifiers\":[],\"buffs\":[]}");
        }

        private static string CreateValidPreviewPackageWithResourceCatalog(
            string container,
            string packageId,
            string address = "mod/title",
            string typeId = ResourceTypeIds.String,
            bool allowOverride = true)
        {
            string dir = Directory.CreateDirectory(Path.Combine(container, packageId.Replace(".", "_"))).FullName;
            CreateValidPreviewPackageAt(dir, packageId, resourceCatalog: "resources/catalog.json");

            string resourcesDir = Directory.CreateDirectory(Path.Combine(dir, "resources")).FullName;
            File.WriteAllText(Path.Combine(resourcesDir, "catalog.json"),
                "{"
                + "\"schemaVersion\":1,"
                + "\"catalogId\":\"" + packageId + ".resources\","
                + "\"packageId\":\"" + packageId + "\","
                + "\"entries\":[{"
                + "\"id\":\"demo.text.title\","
                + "\"type\":\"" + typeId + "\","
                + "\"variant\":\"\","
                + "\"provider\":\"memory\","
                + "\"address\":\"" + address + "\","
                + "\"allowOverride\":" + (allowOverride ? "true" : "false")
                + ",\"providerData\":{}"
                + "}]"
                + "}");
            return dir;
        }

        private static void CreateValidModPackage(string container, string packageId)
        {
            string dir = Directory.CreateDirectory(Path.Combine(container, packageId.Replace(".", "_"))).FullName;
            string runtimeDir = Directory.CreateDirectory(Path.Combine(dir, "runtime")).FullName;
            File.WriteAllText(Path.Combine(dir, "mod.json"),
                "{\"schemaVersion\":1,\"packageId\":\"" + packageId + "\",\"displayName\":\"Test\",\"author\":\"Test\",\"version\":\"0.1.0\",\"kind\":\"Mod\",\"runtimePatch\":\"runtime/patch.json\"}");
            File.WriteAllText(Path.Combine(runtimeDir, "patch.json"),
                "{\"format\":\"mx.runtimeConfigPatch.v1\",\"sourceId\":\"" + packageId + "\",\"layer\":\"Mod\",\"modifiers\":[],\"buffs\":[]}");
        }

        private static string FindKey(RuntimeModPackageCatalog catalog, string packageId)
        {
            for (int i = 0; i < catalog.Items.Count; i++)
            {
                if (string.Equals(catalog.Items[i].Manifest?.PackageId, packageId, StringComparison.Ordinal))
                    return catalog.Items[i].PackageKey;
            }

            return string.Empty;
        }

        // Use full type to avoid ambiguity
    }
}
