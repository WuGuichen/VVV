using System;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class VfxAuthoringRecipePrefabBakerTests
    {
        private const string OutputRoot = "Assets/MxFrameworkGenerated/Vfx/AuthoringBakeTransactionTests";

        [TestCase("write")]
        [TestCase("commit")]
        [TestCase("import")]
        public void PublishEffect_EvidenceFailure_RestoresExistingPrefabAndEvidence(string failure)
        {
            string token = Guid.NewGuid().ToString("N");
            string effectId = "tests.authoring." + token;
            string outputPath = OutputRoot + "/" + token + ".prefab";
            string evidencePath = VfxAuthoringRecipePrefabBaker.GetEvidencePath(effectId);
            EnsureFolder(OutputRoot);
            EnsureFolder(VfxAuthoringRecipePrefabBaker.EvidenceRoot);
            CreatePrefab(outputPath, "old_published_effect", withParticleSystem: false);
            File.WriteAllText(evidencePath, "{\"sentinel\":\"old-evidence\"}\n");
            AssetDatabase.ImportAsset(evidencePath, ImportAssetOptions.ForceSynchronousImport);
            string prefabGuid = AssetDatabase.AssetPathToGUID(outputPath);
            string evidenceGuid = AssetDatabase.AssetPathToGUID(evidencePath);
            byte[] prefabBytes = File.ReadAllBytes(outputPath);
            byte[] evidenceBytes = File.ReadAllBytes(evidencePath);
            int finalImportAttempts = 0;
            var candidate = new GameObject("new_published_effect");
            candidate.AddComponent<ParticleSystem>();
            try
            {
                Action<string, string> writer = failure == "write"
                    ? (_, _) => throw new IOException("simulated evidence write failure")
                    : null;
                Func<string, string, bool> committer = failure == "commit"
                    ? (temporary, output) =>
                    {
                        File.WriteAllText(output, "partial evidence");
                        return false;
                    }
                    : null;
                Func<string, bool> importer = failure == "import"
                    ? path =>
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        if (!string.Equals(path, evidencePath, StringComparison.Ordinal)) return true;
                        finalImportAttempts++;
                        return finalImportAttempts > 1;
                    }
                    : null;

                Assert.Throws<InvalidOperationException>(() => VfxAuthoringRecipePrefabBaker.PublishEffect(
                    candidate, effectId, "recipe.test", "hash", "baker", "1", outputPath,
                    writer, committer, importer));

                Assert.That(File.ReadAllBytes(outputPath), Is.EqualTo(prefabBytes));
                Assert.That(File.ReadAllBytes(evidencePath), Is.EqualTo(evidenceBytes));
                Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.EqualTo(prefabGuid));
                Assert.That(AssetDatabase.AssetPathToGUID(evidencePath), Is.EqualTo(evidenceGuid));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(outputPath).transform.Find("old_published_effect"), Is.Not.Null);
                if (failure == "import") Assert.That(finalImportAttempts, Is.EqualTo(2), "Old evidence must be reimported after rollback.");
                AssertNoTemporaryFiles(outputPath, evidencePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
                DeleteAsset(outputPath);
                DeleteAsset(evidencePath);
            }
        }

        [Test]
        public void PublishEffect_FirstGenerationImportFailure_LeavesNoPublishedAssets()
        {
            string token = Guid.NewGuid().ToString("N");
            string effectId = "tests.authoring." + token;
            string createdOutputRoot = OutputRoot + "/" + token;
            string outputPath = createdOutputRoot + "/Nested/effect.prefab";
            string evidencePath = VfxAuthoringRecipePrefabBaker.GetEvidencePath(effectId);
            EnsureFolder(OutputRoot);
            var candidate = new GameObject("candidate");
            try
            {
                Assert.Throws<InvalidOperationException>(() => VfxAuthoringRecipePrefabBaker.PublishEffect(
                    candidate, effectId, "recipe.test", "hash", "baker", "1", outputPath,
                    evidenceImporter: _ => false));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(outputPath), Is.Null);
                Assert.That(File.Exists(outputPath), Is.False);
                Assert.That(File.Exists(outputPath + ".meta"), Is.False);
                Assert.That(File.Exists(evidencePath), Is.False);
                Assert.That(File.Exists(evidencePath + ".meta"), Is.False);
                Assert.That(AssetDatabase.IsValidFolder(createdOutputRoot), Is.False);
                Assert.That(Directory.Exists(createdOutputRoot), Is.False);
                Assert.That(File.Exists(createdOutputRoot + ".meta"), Is.False);
                AssertNoTemporaryFiles(outputPath, evidencePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
                DeleteAsset(outputPath);
                DeleteAsset(evidencePath);
            }
        }

        [Test]
        public void EvidenceTransaction_FirstGenerationFailure_RemovesCreatedDirectoryTree()
        {
            string token = Guid.NewGuid().ToString("N");
            string createdRoot = OutputRoot + "/EvidenceDirectories/" + token;
            string evidencePath = createdRoot + "/Nested/evidence.json";
            var evidence = new JObject { ["schemaVersion"] = "test" };
            EnsureFolder(OutputRoot + "/EvidenceDirectories");
            using (var transaction = new VfxPublishedEvidenceAssetTransaction(evidencePath, importer: _ => false))
            {
                Assert.That(transaction.Prepare(evidence, out string prepareMessage), Is.True, prepareMessage);
                Assert.That(transaction.Commit(out string commitMessage), Is.False, commitMessage);
            }

            Assert.That(Directory.Exists(createdRoot), Is.False);
            Assert.That(File.Exists(createdRoot + ".meta"), Is.False);
            Assert.That(File.Exists(evidencePath), Is.False);
            Assert.That(File.Exists(evidencePath + ".meta"), Is.False);
        }

        [Test]
        public void EvidenceTransaction_RestoreReimportFailure_ReportsCompositeFailure()
        {
            string token = Guid.NewGuid().ToString("N");
            string evidencePath = OutputRoot + "/" + token + ".json";
            EnsureFolder(OutputRoot);
            File.WriteAllText(evidencePath, "{\"sentinel\":\"old\"}\n");
            AssetDatabase.ImportAsset(evidencePath, ImportAssetOptions.ForceSynchronousImport);
            byte[] original = File.ReadAllBytes(evidencePath);
            try
            {
                using var transaction = new VfxPublishedEvidenceAssetTransaction(evidencePath, importer: _ => false);
                Assert.That(transaction.Prepare(new JObject { ["sentinel"] = "new" }, out string prepareMessage), Is.True, prepareMessage);
                Assert.That(transaction.Commit(out string commitMessage), Is.False);
                Assert.That(commitMessage, Does.Contain("Rollback also failed"));
                Assert.That(commitMessage, Does.Contain("reimport the restored evidence"));
                Assert.That(File.ReadAllBytes(evidencePath), Is.EqualTo(original));
            }
            finally
            {
                DeleteAsset(evidencePath);
            }
        }

        [Test]
        public void PublishEffect_Success_UpdatesSynchronizedEvidenceAndPreservesExistingGuids()
        {
            string token = Guid.NewGuid().ToString("N");
            string effectId = "tests.authoring." + token;
            string outputPath = OutputRoot + "/" + token + ".prefab";
            string evidencePath = VfxAuthoringRecipePrefabBaker.GetEvidencePath(effectId);
            EnsureFolder(OutputRoot);
            EnsureFolder(VfxAuthoringRecipePrefabBaker.EvidenceRoot);
            CreatePrefab(outputPath, "old_published_effect", withParticleSystem: false);
            File.WriteAllText(evidencePath, "{\"sentinel\":\"old-evidence\"}\n");
            AssetDatabase.ImportAsset(evidencePath, ImportAssetOptions.ForceSynchronousImport);
            string prefabGuid = AssetDatabase.AssetPathToGUID(outputPath);
            string evidenceGuid = AssetDatabase.AssetPathToGUID(evidencePath);
            var candidate = new GameObject("new_published_effect");
            var child = new GameObject("particle_layer");
            child.transform.SetParent(candidate.transform, false);
            child.AddComponent<ParticleSystem>();
            try
            {
                VfxAuthoringRecipePrefabBaker.PublishEffect(
                    candidate, effectId, "recipe.test", "input-hash", "baker", "1", outputPath);

                JObject evidence = JObject.Parse(File.ReadAllText(evidencePath));
                GameObject published = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                Assert.That(published.transform.Find("particle_layer"), Is.Not.Null);
                Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.EqualTo(prefabGuid));
                Assert.That(AssetDatabase.AssetPathToGUID(evidencePath), Is.EqualTo(evidenceGuid));
                Assert.That(evidence.Value<string>("outputAssetPath"), Is.EqualTo(outputPath));
                Assert.That(evidence.Value<string>("outputAssetGuid"), Is.EqualTo(prefabGuid));
                Assert.That(evidence.Value<int>("gameObjectCount"), Is.EqualTo(2));
                Assert.That(evidence.Value<int>("particleSystemCount"), Is.EqualTo(1));
                Assert.That(evidence.Value<int>("rendererCount"), Is.EqualTo(1));
                AssertNoTemporaryFiles(outputPath, evidencePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
                DeleteAsset(outputPath);
                DeleteAsset(evidencePath);
            }
        }

        private static void CreatePrefab(string path, string name, bool withParticleSystem)
        {
            var root = new GameObject("published_effect");
            try
            {
                var marker = new GameObject(name);
                marker.transform.SetParent(root.transform, false);
                if (withParticleSystem) marker.AddComponent<ParticleSystem>();
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, path), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertNoTemporaryFiles(string outputPath, string evidencePath)
        {
            string outputDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty;
            string outputStem = Path.GetFileNameWithoutExtension(outputPath);
            if (Directory.Exists(outputDirectory))
                Assert.That(Directory.GetFiles(outputDirectory, "__" + outputStem + "_candidate_tmp*.prefab"), Is.Empty);
            string evidenceDirectory = Path.GetDirectoryName(evidencePath) ?? string.Empty;
            string evidenceName = Path.GetFileName(evidencePath);
            if (Directory.Exists(evidenceDirectory))
                Assert.That(Directory.GetFiles(evidenceDirectory, "." + evidenceName + ".*.tmp"), Is.Empty);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void DeleteAsset(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path)) AssetDatabase.DeleteAsset(path);
        }
    }
}
