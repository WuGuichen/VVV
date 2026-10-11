using System;
using System.IO;
using System.Reflection;
using System.Text;
using MxFramework.Editor.CharacterImport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace MxFramework.Tests.CharacterImport
{
    public class CharacterPreviewGlbCleanupTests
    {
        private string _root;
        private string _asset;
        private Type _cleanup;

        [SetUp]
        public void SetUp()
        {
            _root = "Assets/__PreviewGlbCleanup_" + Guid.NewGuid().ToString("N");
            _asset = _root + "/previews/source-only.glb";
            Directory.CreateDirectory(_root + "/previews");
            Directory.CreateDirectory(_root + "/package_cache");
            string json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"name\":\"PreviewFixture\"}]}";
            while (Encoding.UTF8.GetByteCount(json) % 4 != 0) json += " ";
            byte[] payload = Encoding.UTF8.GetBytes(json);
            using (var stream = File.Create(_asset))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0x46546c67u); writer.Write(2u); writer.Write((uint)(20 + payload.Length));
                writer.Write((uint)payload.Length); writer.Write(0x4e4f534au); writer.Write(payload);
            }
            WriteReport(_asset);
            AssetDatabase.ImportAsset(_root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            Assert.IsTrue(File.Exists(_asset + ".meta"));
            _cleanup = typeof(CharacterPackageImportCommand).Assembly.GetType("MxFramework.Editor.CharacterImport.CharacterPreviewGlbDeferredCleanup");
            Assert.NotNull(_cleanup);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(_root);

        [Test]
        public void DeclaredUnreferencedCandidateIsDeletedWithMeta()
        {
            object result = Execute();
            Assert.AreEqual(1, ReadCount(result, "DeletedCount"),
                string.Join(" | ", (System.Collections.Generic.List<string>)result.GetType().GetField("Messages").GetValue(result)));
            Assert.IsTrue((bool)result.GetType().GetProperty("Succeeded").GetValue(result));
            Assert.IsFalse(File.Exists(_asset));
            Assert.IsFalse(File.Exists(_asset + ".meta"));
        }

        [Test]
        public void ReferenceOutsideGeneratedFoldersBlocksCompletion()
        {
            string guid = AssetDatabase.AssetPathToGUID(_asset);
            File.WriteAllText(_root + "/consumer.json", "{\"guid\":\"" + guid + "\"}");
            AssetDatabase.ImportAsset(_root + "/consumer.json");
            object result = Execute();
            Assert.AreEqual(1, ReadCount(result, "BlockedCount"));
            Assert.IsFalse((bool)result.GetType().GetProperty("Succeeded").GetValue(result));
            Assert.IsTrue(File.Exists(_asset));
            Assert.IsTrue(File.Exists(_asset + ".meta"));
        }

        [Test]
        public void GlbWithoutDeferredOperationIsPreserved()
        {
            WriteReport(null);
            Assert.IsNull(Execute());
            Assert.IsTrue(File.Exists(_asset));
            Assert.IsTrue(File.Exists(_asset + ".meta"));
        }

        [Test]
        public void NativePrefabDependencyBlocksDeletion()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(_asset);
            Assert.NotNull(prefab);
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (UnityEngine.GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                Assert.NotNull(PrefabUtility.SaveAsPrefabAsset(instance, _root + "/consumer.prefab"));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
            object result = Execute();
            Assert.AreEqual(1, ReadCount(result, "BlockedCount"));
            Assert.IsFalse((bool)result.GetType().GetProperty("Succeeded").GetValue(result));
            Assert.IsTrue(File.Exists(_asset));
        }

        [Test]
        public void EscapingDeferredTargetFailsClosed()
        {
            WriteReport("Assets/OtherPackage/previews/other.glb");
            object result = Execute();
            Assert.AreEqual(1, ReadCount(result, "UnverifiedCount"));
            Assert.IsFalse((bool)result.GetType().GetProperty("Succeeded").GetValue(result));
            Assert.IsTrue(File.Exists(_asset));
        }

        private object Execute() => _cleanup.GetMethod("Execute", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { _root });
        private static int ReadCount(object value, string name) => (int)value.GetType().GetField(name).GetValue(value);
        private void WriteReport(string target)
        {
            var operations = new JArray();
            if (target != null) operations.Add(new JObject { ["kind"] = "exportCleanup", ["action"] = "Deferred", ["targetPath"] = target });
            File.WriteAllText(_root + "/package_cache/import_report.json", new JObject { ["operations"] = operations }.ToString());
        }
    }
}
