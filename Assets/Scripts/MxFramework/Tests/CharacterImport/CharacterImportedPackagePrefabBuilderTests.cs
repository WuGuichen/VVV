using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
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

        [Test]
        public void AddAnimationRuntimeResources_IncludesDirectionalRunClipsFromRegistry()
        {
            EnsureTempRoot();
            EnsureFolderPath(ConfigRoot);
            File.WriteAllText(
                ConfigRoot + "/animation_clip_registry.json",
                "{\n"
                + "  \"format\": \"mx.animationClipRegistry.v1\",\n"
                + "  \"schemaVersion\": \"1.0\",\n"
                + "  \"packageId\": \"animation.iron_vanguard\",\n"
                + "  \"clips\": [\n"
                + "    {\n"
                + "      \"setId\": \"set.base\",\n"
                + "      \"groupId\": \"group.locomotion\",\n"
                + "      \"clipId\": \"run.r\",\n"
                + "      \"sourceClipName\": \"standing_run_right\",\n"
                + "      \"runtimeResourceKey\": \"art.character.skeleton.animation.standing_run_right\"\n"
                + "    },\n"
                + "    {\n"
                + "      \"setId\": \"set.base\",\n"
                + "      \"groupId\": \"group.locomotion\",\n"
                + "      \"clipId\": \"run.l\",\n"
                + "      \"sourceClipName\": \"standing_run_left\",\n"
                + "      \"runtimeResourceKey\": \"art.character.skeleton.animation.standing_run_left\"\n"
                + "    },\n"
                + "    {\n"
                + "      \"setId\": \"set.base\",\n"
                + "      \"groupId\": \"group.locomotion\",\n"
                + "      \"clipId\": \"run.b\",\n"
                + "      \"sourceClipName\": \"standing_run_back\",\n"
                + "      \"runtimeResourceKey\": \"art.character.skeleton.animation.standing_run_back\"\n"
                + "    }\n"
                + "  ]\n"
                + "}\n");
            AssetDatabase.ImportAsset(ConfigRoot + "/animation_clip_registry.json", ImportAssetOptions.ForceUpdate);

            MethodInfo method = BuilderType.GetMethod("AddAnimationRuntimeResources", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            Type entryType = BuilderType.GetNestedType("RuntimeResourceBootstrapEntry", BindingFlags.NonPublic);
            Assert.IsNotNull(entryType);
            IList runtimeResources = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
            var runtimeResourceIds = new HashSet<string>(StringComparer.Ordinal);

            method.Invoke(null, new object[] { TempRoot, "iron_vanguard", runtimeResources, runtimeResourceIds });

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "art.character.skeleton.animation.standing_run_right",
                    "art.character.skeleton.animation.standing_run_left",
                    "art.character.skeleton.animation.standing_run_back"
                },
                ReadEntryIds(runtimeResources));
        }

        [Test]
        public void ConfigureRuntimeResourceBootstrap_UsesImportedPackageRootForArtifactPathsAndAddresses()
        {
            EnsureTempRoot();
            string prefabPath = CreatePrefabAsset(TempRoot + "/prefabs/test_package_character_preview.prefab");
            string clipPath = CreateAnimationClipAsset(AssetRoot + "/standing_run_right.anim", "standing_run_right");
            WriteAnimationSetDefinition();
            WriteAnimationClipRegistry(new AnimationRegistryEntry(
                "set.base",
                "group.locomotion",
                "run.r",
                "standing_run_right",
                "art.character.skeleton.animation.standing_run_right",
                clipPath));

            CharacterImportedPackage package = CreatePackage();
            var owner = new GameObject("bootstrap");
            try
            {
                CharacterRuntimeResourceBootstrap bootstrap = owner.AddComponent<CharacterRuntimeResourceBootstrap>();
                InvokeConfigureRuntimeResourceBootstrap(bootstrap, TempRoot, package, prefabPath);

                var serialized = new SerializedObject(bootstrap);
                Assert.AreEqual(ConfigRoot + "/animation_set_definition.json", serialized.FindProperty("_animationSetDefinitionJsonPath").stringValue);
                Assert.AreEqual(ConfigRoot + "/animation_clip_registry.json", serialized.FindProperty("_animationClipRegistryPath").stringValue);
                Assert.IsNotEmpty(serialized.FindProperty("_animationSetDefinitionContentHash").stringValue);
                Assert.IsNotEmpty(serialized.FindProperty("_animationClipRegistryContentHash").stringValue);

                SerializedProperty resources = serialized.FindProperty("_resources");
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "char.test_package.prefab.character_preview",
                        "art.character.skeleton.animation.standing_run_right"
                    },
                    ReadSerializedResourceIds(resources));
                Assert.AreEqual(
                    "MxFrameworkGenerated/CharacterPackages/test_package/prefabs/test_package_character_preview",
                    FindSerializedResourceAddress(resources, "char.test_package.prefab.character_preview"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ConfigureRuntimeResourceBootstrap_ReplacesStaleRegistryDerivedClipSnapshot()
        {
            EnsureTempRoot();
            string prefabPath = CreatePrefabAsset(TempRoot + "/prefabs/test_package_character_preview.prefab");
            string rightClipPath = CreateAnimationClipAsset(AssetRoot + "/standing_run_right.anim", "standing_run_right");
            string leftClipPath = CreateAnimationClipAsset(AssetRoot + "/standing_run_left.anim", "standing_run_left");
            WriteAnimationSetDefinition();
            WriteAnimationClipRegistry(new AnimationRegistryEntry(
                "set.base",
                "group.locomotion",
                "run.r",
                "standing_run_right",
                "art.character.skeleton.animation.standing_run_right",
                rightClipPath));

            CharacterImportedPackage package = CreatePackage();
            var owner = new GameObject("bootstrap");
            try
            {
                CharacterRuntimeResourceBootstrap bootstrap = owner.AddComponent<CharacterRuntimeResourceBootstrap>();
                InvokeConfigureRuntimeResourceBootstrap(bootstrap, TempRoot, package, prefabPath);

                WriteAnimationClipRegistry(
                    new AnimationRegistryEntry(
                        "set.base",
                        "group.locomotion",
                        "run.r",
                        "standing_run_right",
                        "art.character.skeleton.animation.standing_run_right",
                        rightClipPath),
                    new AnimationRegistryEntry(
                        "set.base",
                        "group.locomotion",
                        "run.l",
                        "standing_run_left",
                        "art.character.skeleton.animation.standing_run_left",
                        leftClipPath));
                InvokeConfigureRuntimeResourceBootstrap(bootstrap, TempRoot, package, prefabPath);

                SerializedProperty resources = new SerializedObject(bootstrap).FindProperty("_resources");
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "char.test_package.prefab.character_preview",
                        "art.character.skeleton.animation.standing_run_right",
                        "art.character.skeleton.animation.standing_run_left"
                    },
                    ReadSerializedResourceIds(resources));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
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

        private static void InvokeConfigureRuntimeResourceBootstrap(
            CharacterRuntimeResourceBootstrap bootstrap,
            string importedPackageRoot,
            CharacterImportedPackage package,
            string characterPrefabPath)
        {
            MethodInfo method = BuilderType.GetMethod("ConfigureRuntimeResourceBootstrap", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { bootstrap, importedPackageRoot, package, characterPrefabPath });
        }

        private static T ReadProperty<T>(object obj, string property)
        {
            PropertyInfo info = obj.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(info);
            return (T)info.GetValue(obj);
        }

        private static string[] ReadEntryIds(IList entries)
        {
            var ids = new List<string>();
            foreach (object entry in entries)
                ids.Add(ReadProperty<string>(entry, "Id"));
            return ids.ToArray();
        }

        private static string[] ReadSerializedResourceIds(SerializedProperty resources)
        {
            var ids = new List<string>();
            for (int i = 0; i < resources.arraySize; i++)
                ids.Add(resources.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue);
            return ids.ToArray();
        }

        private static string FindSerializedResourceAddress(SerializedProperty resources, string id)
        {
            for (int i = 0; i < resources.arraySize; i++)
            {
                SerializedProperty entry = resources.GetArrayElementAtIndex(i);
                if (!string.Equals(entry.FindPropertyRelative("_id").stringValue, id, StringComparison.Ordinal))
                    continue;

                return entry.FindPropertyRelative("_address").stringValue;
            }

            return string.Empty;
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

        private static string CreateAnimationClipAsset(string path, string clipName)
        {
            EnsureFolderPath(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var clip = new AnimationClip { name = clipName };
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static void WriteAnimationSetDefinition()
        {
            EnsureFolderPath(ConfigRoot);
            File.WriteAllText(
                ConfigRoot + "/animation_set_definition.json",
                "{\n"
                + "  \"format\": \"mx.animationSetDefinition.v1\",\n"
                + "  \"schemaVersion\": \"1.0\",\n"
                + "  \"packageId\": \"test_package\",\n"
                + "  \"sets\": [\n"
                + "    {\n"
                + "      \"setId\": \"set.base\",\n"
                + "      \"layers\": [],\n"
                + "      \"blend1DDefinitions\": [],\n"
                + "      \"blend2DDefinitions\": [],\n"
                + "      \"blendTrees\": [],\n"
                + "      \"states\": [],\n"
                + "      \"transitions\": [],\n"
                + "      \"actionBindings\": [],\n"
                + "      \"warmup\": [],\n"
                + "      \"timelineEvents\": []\n"
                + "    }\n"
                + "  ]\n"
                + "}\n");
            AssetDatabase.ImportAsset(ConfigRoot + "/animation_set_definition.json", ImportAssetOptions.ForceUpdate);
        }

        private static void WriteAnimationClipRegistry(params AnimationRegistryEntry[] entries)
        {
            EnsureFolderPath(ConfigRoot);
            var lines = new List<string>
            {
                "{",
                "  \"format\": \"mx.animationClipRegistry.v1\",",
                "  \"schemaVersion\": \"1.0\",",
                "  \"packageId\": \"test_package\",",
                "  \"clips\": ["
            };

            for (int i = 0; i < entries.Length; i++)
            {
                AnimationRegistryEntry entry = entries[i];
                string suffix = i == entries.Length - 1 ? string.Empty : ",";
                lines.Add("    {");
                lines.Add("      \"setId\": \"" + entry.SetId + "\",");
                lines.Add("      \"groupId\": \"" + entry.GroupId + "\",");
                lines.Add("      \"clipId\": \"" + entry.ClipId + "\",");
                lines.Add("      \"sourceClipName\": \"" + entry.SourceClipName + "\",");
                lines.Add("      \"runtimeResourceKey\": \"" + entry.RuntimeResourceKey + "\",");
                lines.Add("      \"sourceSelection\": {");
                lines.Add("        \"unityAssetPath\": \"" + entry.UnityAssetPath + "\"");
                lines.Add("      }");
                lines.Add("    }" + suffix);
            }

            lines.Add("  ]");
            lines.Add("}");
            File.WriteAllText(ConfigRoot + "/animation_clip_registry.json", string.Join("\n", lines) + "\n");
            AssetDatabase.ImportAsset(ConfigRoot + "/animation_clip_registry.json", ImportAssetOptions.ForceUpdate);
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

        private readonly struct AnimationRegistryEntry
        {
            public AnimationRegistryEntry(
                string setId,
                string groupId,
                string clipId,
                string sourceClipName,
                string runtimeResourceKey,
                string unityAssetPath)
            {
                SetId = setId;
                GroupId = groupId;
                ClipId = clipId;
                SourceClipName = sourceClipName;
                RuntimeResourceKey = runtimeResourceKey;
                UnityAssetPath = unityAssetPath;
            }

            public string SetId { get; }
            public string GroupId { get; }
            public string ClipId { get; }
            public string SourceClipName { get; }
            public string RuntimeResourceKey { get; }
            public string UnityAssetPath { get; }
        }
    }
}
