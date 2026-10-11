using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using MxFramework.Vfx.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class VfxUnityProjectionPublishTransactionTests
    {
        private const string TestRoot = "Assets/MxFrameworkGenerated/Vfx/ProjectionPublishTransactionTests";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestRoot)) AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [TestCase("import")]
        [TestCase("refresh")]
        public void ExistingProjectionFailure_RestoresBytesMetaGuidAndProfileReference(string failure)
        {
            Fixture fixture = CreateFixture(existingProjection: true, nestedProjection: false);
            ProjectionSnapshot original = Snapshot(fixture);
            int importCount = 0;
            int refreshCount = 0;
            Func<string, bool> importer = failure == "import"
                ? path =>
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    importCount++;
                    return importCount != 1;
                }
                : null;
            Func<bool> refresher = failure == "refresh"
                ? () =>
                {
                    refreshCount++;
                    return refreshCount != 1;
                }
                : () => true;
            using var transaction = new VfxUnityProjectionPublishTransaction(
                fixture.ProjectionPath,
                fixture.Profile,
                importer: importer,
                runtimeRefresher: refresher);

            Assert.That(transaction.Publish("sha256:new-manifest", fixture.NewBindings, out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            if (failure == "import") Assert.That(importCount, Is.EqualTo(2));
            if (failure == "refresh") Assert.That(refreshCount, Is.EqualTo(2));
            AssertRestored(fixture, original);
            AssertNoProvisionalFiles(fixture.CaseRoot);
        }

        [Test]
        public void FirstCreatePublishFailure_RemovesProjectionMetaAndCreatedFolderAndRestoresProfile()
        {
            Fixture fixture = CreateFixture(existingProjection: false, nestedProjection: true);
            VfxPublishedUnityBindingAsset originalReference = fixture.Profile.PublishedVfxBindings;
            using var transaction = new VfxUnityProjectionPublishTransaction(
                fixture.ProjectionPath,
                fixture.Profile,
                publisher: (_, _) => false,
                runtimeRefresher: () => true);

            Assert.That(transaction.Publish("sha256:first-manifest", fixture.NewBindings, out string message), Is.False);
            Assert.That(message, Does.Contain("rolled back"));
            Assert.That(File.Exists(fixture.ProjectionPath), Is.False);
            Assert.That(File.Exists(fixture.ProjectionPath + ".meta"), Is.False);
            Assert.That(Directory.Exists(Path.GetDirectoryName(fixture.ProjectionPath) ?? string.Empty), Is.False);
            Assert.That(fixture.Profile.PublishedVfxBindings, Is.SameAs(originalReference));
            AssertNoProvisionalFiles(fixture.CaseRoot);
        }

        [Test]
        public void Success_PublishesValidatedProjectionPreservesGuidAndUpdatesProfile()
        {
            Fixture fixture = CreateFixture(existingProjection: true, nestedProjection: false);
            ProjectionSnapshot original = Snapshot(fixture);
            using var transaction = new VfxUnityProjectionPublishTransaction(
                fixture.ProjectionPath,
                fixture.Profile,
                runtimeRefresher: () => true);

            Assert.That(transaction.Publish("sha256:new-manifest", fixture.NewBindings, out string message), Is.True, message);
            VfxPublishedUnityBindingAsset published =
                AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(fixture.ProjectionPath);
            Assert.That(published.name, Is.EqualTo(Path.GetFileNameWithoutExtension(fixture.ProjectionPath)));
            Assert.That(published.ManifestHash, Is.EqualTo("sha256:new-manifest"));
            Assert.That(published.Bindings, Has.Count.EqualTo(1));
            Assert.That(published.Bindings[0].ResourceKey, Is.EqualTo(fixture.NewBindings[0].ResourceKey));
            Assert.That(published.Bindings[0].Address, Is.EqualTo(fixture.NewBindings[0].Address));
            Assert.That(published.Bindings[0].Prefab, Is.EqualTo(fixture.NewBindings[0].Prefab));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.ProjectionPath), Is.EqualTo(original.Guid));
            Assert.That(fixture.Profile.PublishedVfxBindings, Is.SameAs(published));
            AssertNoProvisionalFiles(fixture.CaseRoot);
        }

        private static Fixture CreateFixture(bool existingProjection, bool nestedProjection)
        {
            string token = Guid.NewGuid().ToString("N");
            string caseRoot = TestRoot + "/" + token;
            EnsureAssetFolder(caseRoot);
            string oldPrefabPath = caseRoot + "/old.prefab";
            string newPrefabPath = caseRoot + "/new.prefab";
            CreatePrefab(oldPrefabPath, "old-prefab");
            CreatePrefab(newPrefabPath, "new-prefab");
            string projectionPath = nestedProjection
                ? caseRoot + "/Generated/Nested/projection.asset"
                : caseRoot + "/projection.asset";
            VfxPublishedUnityBindingAsset oldProjection = null;
            if (existingProjection)
            {
                oldProjection = ScriptableObject.CreateInstance<VfxPublishedUnityBindingAsset>();
                oldProjection.Configure("sha256:old-manifest", new List<VfxUnityPrefabBinding>
                {
                    CreateBinding("vfx.test.old", "old/address", oldPrefabPath)
                });
                AssetDatabase.CreateAsset(oldProjection, projectionPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(projectionPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }

            string profilePath = caseRoot + "/profile.asset";
            CharacterTestSceneProfile profile = ScriptableObject.CreateInstance<CharacterTestSceneProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            SetProfileReference(profile, oldProjection);
            AssetDatabase.SaveAssets();
            return new Fixture
            {
                CaseRoot = caseRoot,
                ProjectionPath = projectionPath,
                Profile = profile,
                NewBindings = new List<VfxUnityPrefabBinding>
                {
                    CreateBinding("vfx.test.new", "new/address", newPrefabPath)
                }
            };
        }

        private static VfxUnityPrefabBinding CreateBinding(string id, string address, string prefabPath)
        {
            var binding = new VfxUnityPrefabBinding();
            binding.Configure(
                new ResourceKey(id, ResourceTypeIds.GameObject, string.Empty, string.Empty),
                address,
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            return binding;
        }

        private static void CreatePrefab(string path, string name)
        {
            var root = new GameObject(name);
            try { PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static ProjectionSnapshot Snapshot(Fixture fixture)
        {
            return new ProjectionSnapshot
            {
                Bytes = File.ReadAllBytes(fixture.ProjectionPath),
                Meta = File.ReadAllBytes(fixture.ProjectionPath + ".meta"),
                Guid = AssetDatabase.AssetPathToGUID(fixture.ProjectionPath),
                ProfileReference = fixture.Profile.PublishedVfxBindings
            };
        }

        private static void AssertRestored(Fixture fixture, ProjectionSnapshot original)
        {
            Assert.That(File.ReadAllBytes(fixture.ProjectionPath), Is.EqualTo(original.Bytes));
            Assert.That(File.ReadAllBytes(fixture.ProjectionPath + ".meta"), Is.EqualTo(original.Meta));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.ProjectionPath), Is.EqualTo(original.Guid));
            Assert.That(fixture.Profile.PublishedVfxBindings, Is.SameAs(original.ProfileReference));
            VfxPublishedUnityBindingAsset restored =
                AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(fixture.ProjectionPath);
            Assert.That(restored.ManifestHash, Is.EqualTo("sha256:old-manifest"));
            Assert.That(restored.Bindings[0].ResourceKey.Id, Is.EqualTo("vfx.test.old"));
        }

        private static void SetProfileReference(
            CharacterTestSceneProfile profile,
            VfxPublishedUnityBindingAsset projection)
        {
            var serialized = new SerializedObject(profile);
            SerializedProperty property = serialized.FindProperty("_publishedVfxBindings");
            property.objectReferenceValue = projection;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void AssertNoProvisionalFiles(string root)
        {
            Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Any(path => path.IndexOf(".tmp.asset", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private sealed class Fixture
        {
            public string CaseRoot;
            public string ProjectionPath;
            public CharacterTestSceneProfile Profile;
            public List<VfxUnityPrefabBinding> NewBindings;
        }

        private sealed class ProjectionSnapshot
        {
            public byte[] Bytes;
            public byte[] Meta;
            public string Guid;
            public VfxPublishedUnityBindingAsset ProfileReference;
        }
    }
}
