using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Editor.CharacterImport;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterImport
{
    public class CharacterImportedPackagePrefabBuilderTests
    {
        private const string TempRoot = "Assets/MxFrameworkGenerated/CharacterPackages/__prefab_builder_tests";
        private const string ConfigRoot = TempRoot + "/config";
        private const string AssetRoot = TempRoot + "/resources/models";
        private static readonly Type BuilderType = typeof(CharacterImportedPackagePrefabBuilder);

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void ReadResourcePreviewInfos_UsesUnityCatalogAssetAndKeepsMappingOnlyForWrapperPose()
        {
            EnsureTempRoot();
            WriteMapping("char.test.model.body", "characterModel", "Assets/ShouldNotBeConsumed/mapping_only.glb", scaleX: 2f);
            string modelPath = CreatePrefabAsset(AssetRoot + "/catalog_model.prefab");
            CharacterImportedPackage package = CreatePackage(CreateCatalogEntry(
                "char.test.model.body",
                ResourceTypeIds.GameObject,
                modelPath,
                AssetDatabase.AssetPathToGUID(modelPath),
                "Imported",
                "characterModel",
                "character.model"));

            IDictionary resources = InvokeReadResourcePreviewInfos(package);
            object resource = resources["char.test.model.body"];

            Assert.IsNotNull(resource);
            Assert.AreEqual(modelPath, ReadProperty<string>(resource, "AssetPath"));
            Assert.IsTrue(ReadProperty<bool>(resource, "IsImportReady"));
            Assert.AreEqual(2f, ReadProperty<CharacterRuntimePose>(resource, "WrapperPose").Scale.X);
            Assert.AreNotEqual("Assets/ShouldNotBeConsumed/mapping_only.glb", ReadProperty<string>(resource, "AssetPath"));
        }

        [Test]
        public void ReadResourcePreviewInfos_ReportsNotReadyCatalogEntryForPlaceholderDiagnostics()
        {
            EnsureTempRoot();
            WriteMapping("char.test.model.body", "characterModel", "Assets/ShouldNotBeConsumed/mapping_only.glb", scaleX: 1f);
            CharacterImportedPackage package = CreatePackage(CreateCatalogEntry(
                "char.test.model.body",
                ResourceTypeIds.GameObject,
                "Assets/MissingImportedModel.glb",
                "missing-guid",
                "UnityMissing",
                "characterModel",
                "character.model"));

            IDictionary resources = InvokeReadResourcePreviewInfos(package);
            object resource = resources["char.test.model.body"];

            Assert.IsNotNull(resource);
            Assert.IsFalse(ReadProperty<bool>(resource, "IsImportReady"));
            StringAssert.Contains("UnityMissing", ReadProperty<string>(resource, "Diagnostic"));
            StringAssert.Contains("Assets/MissingImportedModel.glb", ReadProperty<string>(resource, "Diagnostic"));
        }

        [Test]
        public void FindResource_DoesNotFallbackWhenExplicitWeaponResourceIsMissing()
        {
            EnsureTempRoot();
            string modelPath = CreatePrefabAsset(AssetRoot + "/weapon_model.prefab");
            CharacterImportedPackage package = CreatePackage(CreateCatalogEntry(
                "char.test.weapon.sword.model",
                ResourceTypeIds.GameObject,
                modelPath,
                AssetDatabase.AssetPathToGUID(modelPath),
                "Imported",
                "weaponModel",
                "weapon.model"));
            IDictionary resources = InvokeReadResourcePreviewInfos(package);

            object missing = InvokeFindResource(resources, "weaponModel", "char.test.weapon.missing.model");
            object fallback = InvokeFindResource(resources, "weaponModel", string.Empty);

            Assert.IsNull(missing);
            Assert.IsNotNull(fallback);
            Assert.AreEqual("char.test.weapon.sword.model", ReadProperty<string>(fallback, "Key"));
        }

        [Test]
        public void ShouldMountWeaponAttachment_RequiresBoundResourceAndGeneratedPrefab()
        {
            var emptyPose = new CharacterRuntimePose(string.Empty, string.Empty, default, default, new CharacterRuntimeVector3(1f, 1f, 1f));
            var removed = new CharacterWeaponAttachmentRuntimeBinding(
                "weapon.test",
                "mainHand",
                "mainHand",
                emptyPose,
                string.Empty,
                string.Empty);
            var bound = new CharacterWeaponAttachmentRuntimeBinding(
                "weapon.test",
                "mainHand",
                "mainHand",
                emptyPose,
                "char.test.weapon.model",
                string.Empty);
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            GameObject prefab = new GameObject("WeaponPrefab");
            try
            {
                Assert.IsFalse(InvokeShouldMountWeaponAttachment(removed, prefabs));
                Assert.IsFalse(InvokeShouldMountWeaponAttachment(bound, prefabs));

                prefabs["mainHand::weapon.test"] = prefab;

                Assert.IsFalse(InvokeShouldMountWeaponAttachment(removed, prefabs));
                Assert.IsTrue(InvokeShouldMountWeaponAttachment(bound, prefabs));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        private static IDictionary InvokeReadResourcePreviewInfos(CharacterImportedPackage package)
        {
            MethodInfo method = BuilderType.GetMethod("ReadResourcePreviewInfos", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (IDictionary)method.Invoke(null, new object[] { package, TempRoot });
        }

        private static object InvokeFindResource(IDictionary resources, string usage, string resourceKey)
        {
            MethodInfo method = BuilderType.GetMethod("FindResource", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return method.Invoke(null, new object[] { resources, usage, resourceKey });
        }

        private static bool InvokeShouldMountWeaponAttachment(
            CharacterWeaponAttachmentRuntimeBinding attachment,
            Dictionary<string, GameObject> weaponPrefabs)
        {
            MethodInfo method = BuilderType.GetMethod("ShouldMountWeaponAttachment", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { attachment, weaponPrefabs });
        }

        private static T ReadProperty<T>(object obj, string property)
        {
            PropertyInfo info = obj.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(info);
            return (T)info.GetValue(obj);
        }

        private static CharacterImportedPackage CreatePackage(params ResourceCatalogEntry[] entries)
        {
            return new CharacterImportedPackage(
                Path.GetFullPath(TempRoot),
                "test_package",
                CharacterImportedConfigSet.Empty,
                CharacterImportedGeometryBinding.Empty,
                CharacterImportedResourceMapping.Empty,
                new ResourceCatalog("character.package.test", "test_package", entries),
                null);
        }

        private static ResourceCatalogEntry CreateCatalogEntry(
            string id,
            string typeId,
            string assetPath,
            string assetGuid,
            string importStatus,
            string usage,
            string label)
        {
            return new ResourceCatalogEntry(
                id,
                typeId,
                "memory",
                assetPath,
                "default",
                "test_package",
                Array.Empty<ResourceKey>(),
                new[] { label },
                "sha256:test",
                0,
                false,
                new Dictionary<string, string>
                {
                    { "packageResourceKey", id },
                    { "unityAssetPath", assetPath },
                    { "unityAssetGuid", assetGuid },
                    { "importStatus", importStatus },
                    { "usage", usage }
                });
        }

        private static string CreatePrefabAsset(string path)
        {
            EnsureFolderPath(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.name = Path.GetFileNameWithoutExtension(path);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Assert.IsNotNull(prefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static void WriteMapping(string resourceKey, string usage, string importTargetPath, float scaleX)
        {
            EnsureFolderPath(ConfigRoot);
            string json = "{\n"
                + "  \"entries\": [\n"
                + "    {\n"
                + "      \"packageResourceKey\": \"" + resourceKey + "\",\n"
                + "      \"usage\": \"" + usage + "\",\n"
                + "      \"importTargetPath\": \"" + importTargetPath + "\",\n"
                + "      \"modelWrapperPose\": {\n"
                + "        \"position\": { \"x\": 0, \"y\": 0, \"z\": 0 },\n"
                + "        \"rotation\": { \"x\": 0, \"y\": 0, \"z\": 0, \"w\": 1 },\n"
                + "        \"scale\": { \"x\": " + scaleX.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"y\": 1, \"z\": 1 }\n"
                + "      }\n"
                + "    }\n"
                + "  ]\n"
                + "}\n";
            File.WriteAllText(ConfigRoot + "/resource_catalog_mapping.json", json);
            AssetDatabase.ImportAsset(ConfigRoot + "/resource_catalog_mapping.json", ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureTempRoot()
        {
            EnsureFolderPath(ConfigRoot);
            EnsureFolderPath(AssetRoot);
        }

        private static void EnsureFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                return;

            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
