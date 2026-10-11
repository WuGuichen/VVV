using System;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class VfxReviewNeededPromotionTransactionTests
    {
        private const string AssetRoot = "Assets/MxFrameworkGenerated/Vfx/ReviewNeededPromotionTransactionTests";
        private static readonly string OverrideRoot =
            Path.GetFullPath("Library/MxFramework/VfxReviewNeededPromotionTransactionTests");

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(AssetRoot);
            if (Directory.Exists(OverrideRoot)) Directory.Delete(OverrideRoot, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [TestCase("catalog-publish-rollback-move-fail")]
        [TestCase("catalog-import")]
        [TestCase("override-publish")]
        [TestCase("runtime-refresh")]
        public void PublishFailure_RestoresSnapshotsOrReportsRecoveryRequired(string failure)
        {
            Fixture fixture = CreateFixture();
            int publishCount = 0;
            int importCount = 0;
            int refreshCount = 0;
            int moveCount = 0;
            using var transaction = new VfxReviewNeededPromotionTransaction(
                fixture.SourcePath,
                fixture.TargetPath,
                fixture.CatalogPath,
                fixture.OverridePath,
                publisher: (temporary, output) =>
                {
                    publishCount++;
                    if (failure == "catalog-publish-rollback-move-fail" && publishCount == 1)
                    {
                        File.WriteAllText(output, "partial catalog");
                        return false;
                    }
                    if (failure == "override-publish" && publishCount == 2)
                    {
                        File.WriteAllText(output, "partial override");
                        return false;
                    }
                    return PublishFile(temporary, output);
                },
                catalogImporter: path =>
                {
                    importCount++;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    return failure != "catalog-import" || importCount > 1;
                },
                runtimeRefresher: () =>
                {
                    refreshCount++;
                    return failure != "runtime-refresh" || refreshCount > 1;
                },
                mover: (source, target) =>
                {
                    moveCount++;
                    if (failure == "catalog-publish-rollback-move-fail" && moveCount == 2)
                        return "simulated rollback move failure";
                    return AssetDatabase.MoveAsset(source, target);
                });

            Assert.That(transaction.Publish(
                fixture.UpdatedCatalog,
                fixture.Overrides,
                fixture.ResourceId,
                fixture.AssetGuid,
                fixture.ContentHash,
                out string message), Is.False);

            if (failure == "catalog-publish-rollback-move-fail")
            {
                Assert.That(message, Does.Contain("RecoveryRequired"));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(fixture.TargetPath), Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(fixture.SourcePath), Is.Null);
            }
            else
            {
                Assert.That(message, Does.Contain("rolled back"));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(fixture.SourcePath), Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(fixture.TargetPath), Is.Null);
            }
            if (failure == "runtime-refresh") Assert.That(refreshCount, Is.EqualTo(2));
            AssertSnapshotRestored(fixture);
            Assert.That(File.Exists(transaction.CatalogTemporaryPath), Is.False);
            Assert.That(File.Exists(transaction.OverrideTemporaryPath), Is.False);
        }

        [Test]
        public void PublishSuccess_CommitsMoveCatalogOverrideAndPreservesGuids()
        {
            Fixture fixture = CreateFixture();
            using var transaction = new VfxReviewNeededPromotionTransaction(
                fixture.SourcePath,
                fixture.TargetPath,
                fixture.CatalogPath,
                fixture.OverridePath,
                runtimeRefresher: () => true);

            Assert.That(transaction.Publish(
                fixture.UpdatedCatalog,
                fixture.Overrides,
                fixture.ResourceId,
                fixture.AssetGuid,
                fixture.ContentHash,
                out string message), Is.True, message);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(fixture.SourcePath), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(fixture.TargetPath), Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.TargetPath), Is.EqualTo(fixture.AssetGuid));
            Assert.That(VfxCompositionDraftService.HashFile(fixture.TargetPath), Is.EqualTo(fixture.ContentHash));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.CatalogPath), Is.EqualTo(fixture.CatalogGuid));
            JObject savedCatalog = JObject.Parse(File.ReadAllText(fixture.CatalogPath));
            Assert.That(JToken.DeepEquals(savedCatalog, fixture.UpdatedCatalog), Is.True);
            VfxPlaytestOverrideDocument savedOverrides = JsonConvert.DeserializeObject<VfxPlaytestOverrideDocument>(
                File.ReadAllText(fixture.OverridePath));
            VfxPlaytestOverrideRecord entry = savedOverrides.Entries.Single();
            Assert.That(entry.CandidateAssetPath, Is.EqualTo(fixture.TargetPath));
            Assert.That(entry.CandidateAssetGuid, Is.EqualTo(fixture.AssetGuid));
            Assert.That(entry.CandidateContentHash, Is.EqualTo(fixture.ContentHash));
            Assert.That(File.Exists(transaction.CatalogTemporaryPath), Is.False);
            Assert.That(File.Exists(transaction.OverrideTemporaryPath), Is.False);
        }

        private static Fixture CreateFixture()
        {
            string token = Guid.NewGuid().ToString("N");
            string caseRoot = AssetRoot + "/" + token;
            EnsureAssetFolder(caseRoot + "/Source");
            string sourcePath = caseRoot + "/Source/candidate.prefab";
            var candidate = new GameObject("candidate");
            PrefabUtility.SaveAsPrefabAsset(candidate, sourcePath);
            UnityEngine.Object.DestroyImmediate(candidate);
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
            string assetGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            string contentHash = VfxCompositionDraftService.HashFile(sourcePath);
            string resourceId = "vfx.test.review-needed." + token;
            string catalogPath = caseRoot + "/catalog.json";
            JObject originalCatalog = CreateCatalog(resourceId, null);
            File.WriteAllText(catalogPath, originalCatalog.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceSynchronousImport);
            string catalogGuid = AssetDatabase.AssetPathToGUID(catalogPath);
            string targetPath = caseRoot + "/Review/Nested/candidate.prefab";
            JObject updatedCatalog = CreateCatalog(resourceId, new JObject
            {
                ["assetPath"] = targetPath,
                ["assetGuid"] = assetGuid,
                ["assetContentHash"] = contentHash
            });
            var overrides = new VfxPlaytestOverrideDocument();
            overrides.Entries.Add(new VfxPlaytestOverrideRecord
            {
                ResourceKey = new VfxPublishedResourceKey { Id = resourceId, TypeId = "GameObject" },
                CandidateAssetPath = targetPath,
                CandidateAssetGuid = assetGuid,
                CandidateContentHash = contentHash
            });
            string overridePath = Path.Combine(OverrideRoot, token, "overrides.json");
            Directory.CreateDirectory(Path.GetDirectoryName(overridePath));
            byte[] overrideBytes = System.Text.Encoding.UTF8.GetBytes("{\"generation\":\"old\"}");
            File.WriteAllBytes(overridePath, overrideBytes);
            return new Fixture
            {
                SourcePath = sourcePath,
                TargetPath = targetPath,
                CatalogPath = catalogPath,
                OverridePath = overridePath,
                ResourceId = resourceId,
                AssetGuid = assetGuid,
                ContentHash = contentHash,
                CatalogGuid = catalogGuid,
                CatalogBytes = File.ReadAllBytes(catalogPath),
                CatalogMetaBytes = File.ReadAllBytes(Path.GetFullPath(catalogPath + ".meta")),
                OverrideBytes = overrideBytes,
                UpdatedCatalog = updatedCatalog,
                Overrides = overrides
            };
        }

        private static JObject CreateCatalog(string resourceId, JObject published)
        {
            return new JObject
            {
                ["effects"] = new JArray(new JObject
                {
                    ["resourceKey"] = new JObject { ["id"] = resourceId },
                    ["published"] = published,
                    ["visualMaturity"] = "Candidate",
                    ["reviewStatus"] = "ReviewNeeded",
                    ["approval"] = null
                })
            };
        }

        private static void AssertSnapshotRestored(Fixture fixture)
        {
            Assert.That(File.ReadAllBytes(fixture.CatalogPath), Is.EqualTo(fixture.CatalogBytes));
            Assert.That(File.ReadAllBytes(Path.GetFullPath(fixture.CatalogPath + ".meta")), Is.EqualTo(fixture.CatalogMetaBytes));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.CatalogPath), Is.EqualTo(fixture.CatalogGuid));
            Assert.That(File.ReadAllBytes(fixture.OverridePath), Is.EqualTo(fixture.OverrideBytes));
        }

        private static bool PublishFile(string temporaryPath, string outputPath)
        {
            if (File.Exists(outputPath)) File.Replace(temporaryPath, outputPath, null, true);
            else File.Move(temporaryPath, outputPath);
            return true;
        }

        private static void EnsureAssetFolder(string path)
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

        private sealed class Fixture
        {
            internal string SourcePath;
            internal string TargetPath;
            internal string CatalogPath;
            internal string OverridePath;
            internal string ResourceId;
            internal string AssetGuid;
            internal string ContentHash;
            internal string CatalogGuid;
            internal byte[] CatalogBytes;
            internal byte[] CatalogMetaBytes;
            internal byte[] OverrideBytes;
            internal JObject UpdatedCatalog;
            internal VfxPlaytestOverrideDocument Overrides;
        }
    }
}
