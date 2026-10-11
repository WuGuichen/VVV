using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;

namespace MxFramework.Tests.Resources
{
    public class GeneratedResourceBuildArtifactLoaderTests
    {
        [Test]
        public void PreloadAndDependencyArtifacts_RoundTrip()
        {
            GeneratedResourcePreloadGroupCatalog groups = GeneratedResourcePreloadGroupLoader.LoadFromJson(@"{
  ""schemaVersion"":1,""profileId"":""global.default"",""catalogId"":""global.runtime"",""mappingInputFingerprint"":""sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"",""buildPlanFingerprint"":""sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"",
  ""groups"":[{""id"":""boot.base"",""explicitKeys"":[{""id"":""boot.text"",""type"":""String"",""variant"":"""",""packageId"":""samples""}],""labels"":[],""failFast"":true,""maxConcurrentLoads"":2}]}" );
            GeneratedAssetBundleDependencyManifest dependencies = GeneratedAssetBundleDependencyManifestLoader.LoadFromJson(@"{
  ""schemaVersion"":1,""profileId"":""global.default"",""buildTarget"":""StandaloneOSX"",""mappingInputFingerprint"":""sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"",""buildPlanFingerprint"":""sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"",
  ""bundles"":[{""bundleName"":""boot"",""dependencies"":[""shared""]}]}" );
            Assert.True(groups.TryGetPlan("boot.base", out ResourcePreloadPlan plan));
            Assert.AreEqual(1, plan.ExplicitKeys.Count);
            Assert.AreEqual("shared", dependencies.CreateDependencyProvider().GetDependencies("boot")[0]);
        }

        [Test]
        public void RuntimeBootstrap_LoadsAndReleasesConcreteExternalCatalogEntry()
        {
            ResourceCatalog primary = new ResourceCatalog("global.runtime", "samples", Array.Empty<ResourceCatalogEntry>());
            GeneratedResourcePreloadGroupCatalog groups = new GeneratedResourcePreloadGroupCatalog("global.default", "global.runtime", Array.Empty<ResourcePreloadPlan>());
            GeneratedAssetBundleDependencyManifest dependencies = GeneratedAssetBundleDependencyManifestLoader.LoadFromJson(@"{
  ""schemaVersion"":1,""profileId"":""global.default"",""buildTarget"":""StandaloneOSX"",""bundles"":[]}" );
            var provider = new MemoryResourceProvider().Register("external/value", "mounted");
            ResourceCatalog external = new ResourceCatalog("external.catalog", "samples", new[]
            {
                new ResourceCatalogEntry("external.value", ResourceTypeIds.String, MemoryResourceProvider.Id, "external/value", packageId: "samples")
            });
            string fingerprint = ResourceCatalogCanonicalFingerprint.Compute(external);
            string mountJson = @"{
  ""schemaVersion"":1,""mappingInputFingerprint"":""sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"",""buildPlanFingerprint"":""sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"",
  ""mounts"":[{""catalogMountId"":""external.mount"",""catalogId"":""external.catalog"",""bootstrapKind"":""CompositionRoot"",""relativeCatalogPath"":"""",""mountOrder"":1,""expectedCatalogFingerprint"":""FINGERPRINT"",""allowlistedKeys"":[{""id"":""external.value"",""type"":""String"",""variant"":"""",""packageId"":""samples"",""allowOverride"":false}]}]}".Replace("FINGERPRINT", fingerprint);
            ResolvedResourceCatalogMountSet mounted = GeneratedResourceCatalogMountLoader.ResolveFromJson(
                mountJson, "Temp", new[] { external }, new[] { MemoryResourceProvider.Id });
            Assert.Throws<ResourceCatalogException>(() => GeneratedResourceCatalogMountLoader.ResolveFromJson(
                mountJson.Replace("\"allowOverride\":false", "\"allowOverride\":true"), "Temp", new[] { external }, new[] { MemoryResourceProvider.Id }));
            var undeclared = new ResourceCatalog("undeclared.catalog", "samples", Array.Empty<ResourceCatalogEntry>());
            Assert.Throws<ResourceCatalogException>(() => GeneratedResourceCatalogMountLoader.ResolveFromJson(
                mountJson, "Temp", new[] { external, undeclared }, new[] { MemoryResourceProvider.Id }));

            GlobalResourceRuntimeBootstrapResult result = GlobalResourceRuntimeBootstrap.Create(
                primary, groups, dependencies, "Temp/ResourceMappingBootstrap", "StandaloneOSX",
                new[] { provider }, mounted);
            ResourceLoadResult<ResourceHandle<string>> load = result.ResourceManager.Load<string>(new ResourceKey("external.value", ResourceTypeIds.String, packageId: "samples"));
            Assert.True(load.Success, load.Error.Message);
            Assert.AreEqual("mounted", load.Value.Value);
            result.ResourceManager.Release(load.Value);
            Assert.AreEqual(1, provider.ReleaseCount);
        }

        [Test]
        public void CatalogMountLoader_AppliesAllowlistAndRejectsFingerprintMismatch()
        {
            string root = Path.Combine("Temp", "MxFrameworkCatalogMountLoaderTests");
            string catalogPath = Path.Combine(root, "ExternalCatalogs", "concrete.json");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath));
            File.WriteAllText(catalogPath, @"{
  ""schemaVersion"":1,""catalogId"":""external.catalog"",""packageId"":""samples"",
  ""entries"":[
    {""id"":""allowed"",""type"":""String"",""provider"":""memory"",""address"":""allowed"",""packageId"":""samples""},
    {""id"":""hidden"",""type"":""String"",""provider"":""memory"",""address"":""hidden"",""packageId"":""samples""}
  ]}" );
            ResourceCatalog concrete = StreamingResourceCatalogLoader.LoadFromFile(catalogPath);
            string fingerprint = ResourceCatalogCanonicalFingerprint.Compute(concrete);
            string manifest = @"{
  ""schemaVersion"":1,""mappingInputFingerprint"":""sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"",""buildPlanFingerprint"":""sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"",""mounts"":[{
    ""catalogMountId"":""external.mount"",""catalogId"":""external.catalog"",""bootstrapKind"":""StreamingAssetsCatalog"",
    ""relativeCatalogPath"":""ExternalCatalogs/concrete.json"",""mountOrder"":10,""expectedCatalogFingerprint"":""FINGERPRINT"",
    ""allowlistedKeys"":[{""id"":""allowed"",""type"":""String"",""variant"":"""",""packageId"":""samples"",""allowOverride"":false}]
  }]}".Replace("FINGERPRINT", fingerprint);
            try
            {
                ResolvedResourceCatalogMountSet resolved = GeneratedResourceCatalogMountLoader.ResolveFromJson(manifest, root, null, new[] { MemoryResourceProvider.Id });
                Assert.AreEqual(1, resolved.Catalogs.Count);
                Assert.AreEqual(1, resolved.Catalogs[0].Entries.Count);
                Assert.AreEqual("allowed", resolved.Catalogs[0].Entries[0].Id);
                string invalid = manifest.Replace(fingerprint, "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd");
                Assert.Throws<ResourceCatalogException>(() => GeneratedResourceCatalogMountLoader.ResolveFromJson(invalid, root, null, new[] { MemoryResourceProvider.Id }));
                Assert.Throws<ResourceCatalogException>(() => GeneratedResourceCatalogMountLoader.ResolveFromJson(manifest, root, null, Array.Empty<string>()));
                string duplicateAllowlist = manifest.Replace("]\n  }]}", ",{\"id\":\"allowed\",\"type\":\"String\",\"variant\":\"\",\"packageId\":\"samples\",\"allowOverride\":false}]\n  }]}");
                Assert.Throws<ResourceCatalogException>(() => GeneratedResourceCatalogMountLoader.ResolveFromJson(duplicateAllowlist, root, null, new[] { MemoryResourceProvider.Id }));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ArtifactSetLoader_RejectsPartialOrMixedGeneration()
        {
            string root = Path.Combine("Temp", "MxFrameworkArtifactSetTests");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            try
            {
                string[] names = { "catalog.json", "mounts.json", "preload.json", "dependencies.json" };
                string[] kinds = { "catalog", "catalogMounts", "preloadGroups", "bundleDependencies" };
                var artifacts = new List<string>();
                for (int i = 0; i < names.Length; i++)
                {
                    string path = Path.Combine(root, names[i]);
                    File.WriteAllText(path, "{}\n");
                    artifacts.Add("{\"kind\":\"" + kinds[i] + "\",\"relativePath\":\"" + names[i] + "\",\"sha256\":\"" + ComputeSha256(path) + "\"}");
                }
                string manifestPath = Path.Combine(root, "set.json");
                File.WriteAllText(manifestPath, "{\"schemaVersion\":1,\"profileId\":\"p\",\"catalogId\":\"c\",\"buildTarget\":\"StandaloneOSX\",\"mappingInputFingerprint\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"buildPlanFingerprint\":\"sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"sourceInputFingerprint\":\"sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc\",\"artifacts\":[" + string.Join(",", artifacts) + "]}");
                Assert.AreEqual("StandaloneOSX", GeneratedResourceArtifactSetLoader.LoadFromFile(manifestPath, root).BuildTarget);
                File.WriteAllText(Path.Combine(root, "catalog.json"), "mixed\n");
                Assert.Throws<ResourceCatalogException>(() => GeneratedResourceArtifactSetLoader.LoadFromFile(manifestPath, root));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static string ComputeSha256(string path)
        {
            using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path);
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        [Test]
        public void RuntimeBootstrap_RegistersSceneProviderForSceneCatalogEntries()
        {
            ResourceCatalog catalog = new ResourceCatalog("global.runtime", "samples", new[]
            {
                new ResourceCatalogEntry("scene.test", ResourceTypeIds.Scene, SceneAssetBundleProvider.Id, "scene.bundle|Assets/Scenes/CharacterTestScene.unity")
            });
            GeneratedResourcePreloadGroupCatalog groups = new GeneratedResourcePreloadGroupCatalog("global.default", "global.runtime", Array.Empty<ResourcePreloadPlan>());
            GeneratedAssetBundleDependencyManifest dependencies = GeneratedAssetBundleDependencyManifestLoader.LoadFromJson(@"{
  ""schemaVersion"":1,""profileId"":""global.default"",""buildTarget"":""StandaloneOSX"",""bundles"":[{""bundleName"":""scene.bundle"",""dependencies"":[]}]}" );
            GlobalResourceRuntimeBootstrapResult result = GlobalResourceRuntimeBootstrap.Create(catalog, groups, dependencies, "Temp/ResourceMappingBootstrap", "StandaloneOSX");
            Assert.NotNull(result.SceneAssetBundleProvider);
            Assert.True(result.ResourceManager.Contains(new ResourceKey("scene.test", ResourceTypeIds.Scene, packageId: "samples")));
        }
    }
}
